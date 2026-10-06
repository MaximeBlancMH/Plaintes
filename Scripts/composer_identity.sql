/* ============================================================================================
   composer.NUMDETAIL : passage en colonne IDENTITY (auto-incrément)

   Pourquoi : EF envoyait NUMDETAIL = 0 pour chaque détail créé, d'où l'erreur
   « Violation of PRIMARY KEY constraint 'PK_Composer' ... The duplicate key value is (0) ».
   Le mapping EF (AppDbContext) est passé à ValueGeneratedOnAdd : la base doit donc générer la clé.

   SQL Server ne permet pas d'ajouter IDENTITY à une colonne existante : on recrée la table
   (sauvegarde, recréation, recopie des lignes avec leurs numéros actuels).

   À faire :
     1. Arrêter l'application.
     2. Exécuter l'ÉTAPE 0 seule, et comparer son résultat avec le schéma supposé à l'étape 2
        (types, valeur par défaut, autres index). Adapter l'étape 2 si besoin.
     3. Exécuter les étapes 1 à 4 d'un seul bloc (tout est dans une transaction : au moindre
        problème, rien n'est modifié).
     4. Redémarrer l'application. La table composer_sauvegarde peut être supprimée plus tard.
   ============================================================================================ */


/* ---------- ÉTAPE 0 : état actuel (lecture seule) ---------- */

-- Colonnes, types, nullabilité, IDENTITY, valeurs par défaut
SELECT c.name AS colonne, t.name AS type, c.max_length, c.is_nullable, c.is_identity,
       dc.name AS contrainte_defaut, dc.definition AS valeur_defaut
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('dbo.composer')
ORDER BY c.column_id;

-- Index et contraintes (la clé primaire PK_Composer et d'éventuels autres index)
SELECT i.name AS index_name, i.type_desc, i.is_primary_key, i.is_unique
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.composer') AND i.name IS NOT NULL;

-- Tables qui référencent composer (doit être VIDE : sinon, elles bloqueraient la suppression)
SELECT fk.name AS cle_etrangere, OBJECT_NAME(fk.parent_object_id) AS table_qui_reference
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id = OBJECT_ID('dbo.composer');

-- Contenu : nombre de lignes et valeurs de clé
SELECT COUNT(*) AS nb_lignes, MIN(NUMDETAIL) AS cle_min, MAX(NUMDETAIL) AS cle_max,
       SUM(CASE WHEN NUMDETAIL = 0 THEN 1 ELSE 0 END) AS lignes_cle_0
FROM dbo.composer;


/* ---------- ÉTAPES 1 à 4 : à exécuter d'un seul bloc ---------- */

SET XACT_ABORT ON;   -- toute erreur annule la transaction entière
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID('dbo.composer'))
    THROW 50001, 'Une table référence composer : voir l''étape 0 avant de continuer.', 1;
IF OBJECT_ID('dbo.composer_sauvegarde') IS NOT NULL
    THROW 50002, 'composer_sauvegarde existe déjà : le script a peut-être déjà été exécuté. Vérifiez avant de continuer.', 1;

-- 1. Sauvegarde de la table (copie simple des données)
SELECT NUMDETAIL, CODENUISANCE, NUMPLAINTE, COUT, SUPPRDET
INTO dbo.composer_sauvegarde
FROM dbo.composer;

-- 2. Recréation de la table, NUMDETAIL en IDENTITY (supprimer l'ancienne table supprime aussi ses contraintes)
DROP TABLE dbo.composer;

CREATE TABLE dbo.composer
(
    NUMDETAIL    int IDENTITY(1,1) NOT NULL,
    CODENUISANCE varchar(5)        NOT NULL,
    NUMPLAINTE   int               NOT NULL,
    COUT         int               NULL,
    SUPPRDET     bit               NOT NULL CONSTRAINT DF_Composer_SupprDet DEFAULT (0),
    CONSTRAINT PK_Composer PRIMARY KEY CLUSTERED (NUMDETAIL),
    CONSTRAINT FK_Composer_Nuisance FOREIGN KEY (CODENUISANCE) REFERENCES dbo.nuisance (CODENUISANCE),
    CONSTRAINT FK_Composer_Plainte  FOREIGN KEY (NUMPLAINTE)   REFERENCES dbo.plainte (NUMPLAINTE)
);

-- 3. Recopie des lignes avec leurs numéros actuels ; l'auto-incrément reprendra après le plus grand.
SET IDENTITY_INSERT dbo.composer ON;
INSERT INTO dbo.composer (NUMDETAIL, CODENUISANCE, NUMPLAINTE, COUT, SUPPRDET)
SELECT NUMDETAIL, CODENUISANCE, NUMPLAINTE, COUT, SUPPRDET
FROM dbo.composer_sauvegarde
ORDER BY NUMDETAIL;
SET IDENTITY_INSERT dbo.composer OFF;

-- 4. Contrôles : même nombre de lignes, colonne bien en IDENTITY
IF (SELECT COUNT(*) FROM dbo.composer) <> (SELECT COUNT(*) FROM dbo.composer_sauvegarde)
    THROW 50003, 'Le nombre de lignes recopiées ne correspond pas : annulation.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.composer'), 'NUMDETAIL', 'IsIdentity') <> 1
    THROW 50004, 'NUMDETAIL n''est pas en IDENTITY : annulation.', 1;

COMMIT TRANSACTION;

-- Vérification : la clé suivante doit être supérieure à la plus grande clé existante
SELECT IDENT_CURRENT('dbo.composer') AS derniere_cle_attribuee, (SELECT MAX(NUMDETAIL) FROM dbo.composer) AS cle_max;


/* ---------- Plus tard, une fois l'application vérifiée ----------
DROP TABLE dbo.composer_sauvegarde;
---------------------------------------------------------------- */
