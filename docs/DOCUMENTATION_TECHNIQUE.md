# Documentation technique : application « Plaintes »

Application web interne de **Mâcon Habitat** pour saisir, suivre et exporter les dossiers de plainte (dégradations, incendies, tags, dépôts d'ordures…).

> **État de ce document** : mis à jour le 08/10/2026 à partir du code du dépôt (branche `master`).
> Les §1 à 6 décrivent l'application ; les §7 à 12 décrivent **ce qui a été fait** pour la conteneuriser et l'exploiter, avec les essais réalisés. Ce qui n'a pas pu être vérifié est signalé « *non vérifié* » (§7.8).

---

## Table des matières

1. [À lire en premier](#1-à-lire-en-premier)
2. [Présentation](#2-présentation)
3. [Architecture](#3-architecture)
4. [Fonctionnement des briques importantes](#4-fonctionnement-des-briques-importantes)
5. [Base de données](#5-base-de-données)
6. [Configuration](#6-configuration)
7. [Conteneurisation : ce qui a été mis en place](#7-conteneurisation--ce-qui-a-été-mis-en-place)
8. [Déploiement et exploitation](#8-déploiement-et-exploitation)
9. [Problèmes rencontrés et dépannage](#9-problèmes-rencontrés-et-dépannage)
10. [Développement local et tests](#10-développement-local-et-tests)
11. [Dette technique : corrigé et restant](#11-dette-technique--corrigé-et-restant)
12. [Annexes](#12-annexes)

---

## 1. À lire en premier

- L'application tourne **dans un conteneur Docker** : l'image contient le code, **LibreOffice** (conversion Word → PDF), les polices et le modèle Word. **Aucune donnée** n'y est stockée.
- Les données vivent à trois endroits : la **base SQL Server** (hors Docker), le **dossier des pièces jointes** (volume) et les **clés de chiffrement des sessions** (volume).
- Le conteneur parle en **HTTP** ; le **HTTPS** est assuré par un reverse proxy placé devant. Toute la configuration passe par des **variables d'environnement**.
- Les fichiers du déploiement sont à la racine du dépôt : `Plaintes/Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`.

Points encore ouverts (détail au [§11](#11-dette-technique--corrigé-et-restant)) :

| # | Point | Effet |
|---|---|---|
| 1 | Certificat SAML versionné dans git (`Sustainsys.Saml2.Tests2.pfx`) | à remplacer par un certificat dédié, hors de git |
| 2 | Chaîne de connexion avec mot de passe dans `AppDbContext.OnConfiguring` | à supprimer |
| 3 | `/Dossiers/{id}/delete` accessible en GET | un lien piégé peut supprimer un dossier |

---

## 2. Présentation

### Ce que fait l'application

- **Dossiers** : liste paginée, filtrée et triable des plaintes ; création, modification, suppression logique.
- **Pièces jointes** : photos, PDF, Word, stockés **sur un disque** (pas en base) avec une empreinte SHA-256.
- **Export PDF** : un seul PDF avec le dépôt de plainte (généré depuis un modèle Word) suivi de toutes les pièces jointes.
- **Gestion des utilisateurs** (rôles A et S) : ajout, modification, suppression logique.
- **Gestion des tables** (rôles A et S) : cités, rues, bâtiments, parties communes, catégories, sous-catégories, nuisances.
- **Statistiques** (tous les rôles) : chiffres clés, évolution (courbe, comparée à l'année précédente), répartition par secteur, catégorie et état d'avancement, cités les plus touchées, nuisances les plus fréquentes. Les graphiques sont dessinés en SVG, sans librairie externe.
- **Connexion** par compte Microsoft (SAML, Entra ID). Un utilisateur doit **exister dans la base** pour entrer.

### Droits

Stockés dans `utilisateur.ACCESS` (1 caractère) et transformés en rôle à la connexion :

| Lettre | Droit | Accès |
|---|---|---|
| `R` | Lecture | dossiers |
| `W` | Écriture | dossiers |
| `A` | Administrateur | + gestion des utilisateurs et des tables |
| `S` | Super administrateur | idem `A` |

Un utilisateur dont `DATEFINU` est renseignée est considéré **supprimé** : il n'entre plus. Un compte inconnu ou supprimé est envoyé vers la page « accès refusé ».

---

## 3. Architecture

### Technologies

| Élément | Choix |
|---|---|
| Plateforme | .NET 10, ASP.NET Core MVC (pages Razor, Bootstrap) |
| Base de données | SQL Server, Entity Framework Core 10 (base **existante**, sans migrations) |
| Authentification | SAML 2.0 avec Microsoft Entra ID (bibliothèque Sustainsys.Saml2) ; cookie de session |
| Logs | Serilog : console (`docker logs`) et fichier `Logs/log-AAAAMMJJ.txt` (30 jours conservés) |
| Word | DocumentFormat.OpenXml (remplissage du modèle) |
| PDF | PDFsharp (assemblage) + **LibreOffice** (conversion Word → PDF) |
| Tests | xUnit, EF Core InMemory |
| Déploiement | Docker (image Debian + .NET 10), Docker Compose |

### Solution

```
Plaintes.slnx
├── Plaintes/                  application web
│   ├── Controllers/           Account, Dossier, Home
│   ├── Services/              PlainteSrv, UtilisateurSrv, ComptesClaims
│   │   ├── Export/            export PDF (orchestration, LibreOffice, assemblage)
│   │   └── Stockage/          écriture / lecture des pièces sur disque
│   ├── Models/                entités EF + modèles de vue
│   ├── Data/                  AppDbContext
│   ├── Views/                 pages Razor
│   ├── Templates/             TemplatePlainte.docx (modèle du dépôt de plainte)
│   ├── Program.cs             configuration de l'application
│   └── Dockerfile             construction de l'image
├── Plaintes.Tests/            tests
├── Scripts/                   architecture.sql (création des tables, généré depuis la base)
├── docs/                      cette documentation
├── docker-compose.yml         déploiement (application + volumes)
├── .env.example               modèle des variables du déploiement
└── .dockerignore              ce qui n'entre pas dans l'image
```

### Services enregistrés (`Program.cs`)

| Service | Durée de vie | Pourquoi |
|---|---|---|
| `IPlainteSrv`, `IUtilisateurSrv`, `IRecuperateurPieces`, `IAssembleurPdf`, `IExportPlainteSrv` | Scoped (par requête) | utilisent la connexion à la base de la requête |
| `IPieceStorage` | Singleton | sans état, un seul accès au disque |
| `IConvertisseurPdf` (LibreOffice) | **Singleton** | les « places » et leurs verrous doivent être partagés par toutes les requêtes |

### Démarrage

Au lancement, l'application **vérifie le stockage des pièces** (`CheckAsync`) : dossier configuré, fichier témoin présent, écriture puis relecture possibles. Si ça échoue, **elle refuse de démarrer** (voulu : on ne veut pas écrire des pièces au mauvais endroit). La base n'est pas testée au démarrage.

---

## 4. Fonctionnement des briques importantes

### 4.1 Authentification

1. Un accès à une page protégée déclenche un *challenge* SAML : redirection vers Microsoft.
2. Retour sur l'application (`/Saml2/Acs`). À ce moment (`OnSigningIn`), l'application cherche l'utilisateur par **e-mail** dans la table `utilisateur`.
3. Utilisateur actif : claims `Name`, `Role` (la lettre), `Email`. Au **premier passage**, son nom (vide à la création) est rempli depuis Microsoft.
4. Utilisateur inconnu ou supprimé : claim `compte_refuse`. Toute page protégée le renvoie vers `/Account/AccesRefuse`.
5. La session est un **cookie** chiffré avec les clés de protection des données (voir §7.4 et §7.6).

> **Session et droits à jour.** À chaque requête, l'application relit l'état de l'utilisateur du cookie dans la base (`ComptesClaims.EtatSessionAsync`, appelée par `OnValidatePrincipal`). S'il a été supprimé, la session est rejetée et le **cookie supprimé** ; il repasse par la connexion Microsoft et arrive sur la page « accès refusé ». Si son **droit** a changé en base (R, W, A, S), le droit du cookie est mis à jour : pas besoin de se déconnecter puis reconnecter. Le résultat est gardé en mémoire **10 secondes** par utilisateur (pour ne pas interroger la base à chaque image ou script) : un changement peut donc mettre jusqu'à 10 secondes à se voir. Un compte déjà « refusé » (inconnu à la connexion) n'est pas revérifié : il doit se reconnecter après son ajout en base.

### 4.2 Stockage des pièces jointes

- Fichier écrit à `{Storage:Racine}/{numPlainte}/{guid}.{extension}`, jamais sous son nom d'origine.
- En base (`piece_jointe`) : nom d'origine, taille lisible, **chemin relatif**, **empreinte SHA-256**, nature.
- Contrôles : extensions autorisées, 20 Mo par fichier, 50 Mo et 10 pièces par plainte.
- Protection contre les chemins `../` : tout chemin est résolu puis vérifié à l'intérieur de la racine.
- **Fichier témoin** (`Storage:FichierTemoin`) : si la racine est un partage réseau ou un volume, ce fichier prouve qu'il est bien monté. Sans lui, un volume absent ferait écrire les pièces dans le conteneur (perdues au prochain remplacement).
- Anciennes pièces : contenu dans la colonne `FICHIER` (base), toujours lisibles.
- L'empreinte est enregistrée mais **aucune vérification ne la relit pour l'instant**.

### 4.3 Export PDF

Chemin : `DossierController.TelechargerPlaintePdf` → `ExportPlainteSrv.ExporterPdfAsync`.

```
1. Word de la plainte fabriqué en mémoire (modèle + valeurs)
2. Conversion en PDF lancée (LibreOffice)          ← en arrière-plan
3. Lecture des pièces jointes (RecuperateurPieces)
4. Conversion lancée pour chaque pièce Word        ← en parallèle (2 à la fois)
5. Attente de toutes les conversions
6. Assemblage (PDFsharp) : plainte + pièces dans l'ordre
```

- **PDF** : repris tels quels. **Images** : une page A4 chacune, agrandie au maximum. **Word** : convertis.
- Une pièce **inutilisable** (introuvable, vide, type inconnu, PDF protégé, conversion échouée) n'arrête pas l'export : elle est ignorée et journalisée.
- **LibreOffice** est lancé comme un processus (`soffice --headless --convert-to pdf`). Chaque conversion a son **dossier de travail** temporaire, supprimé ensuite.
- **Profils** : LibreOffice garde ses réglages dans un « profil ». Le créer coûte ≈ 5,5 s, le réutiliser ≈ 2,8 s *(mesuré sur le poste de développement)*. Les profils sont donc **conservés** dans `{Export:DossierTemporaire}/plaintes-lo-profil-N`.
- **Places** : deux LibreOffice ne peuvent pas partager un profil. On a `Export:ConversionsSimultanees` places (2 par défaut) ; un **sémaphore** limite les conversions simultanées, et un **fichier verrou** (`.verrou`) empêche deux processus (deux instances, tests) de prendre le même profil.
- Après un échec de conversion, le profil de la place est supprimé (il sera recréé).
- Sans volume pour ce dossier, **la première conversion après un remplacement du conteneur coûte ≈ 3 s de plus** (profil à recréer).

### 4.4 Gestion des tables

Suppression :
- **cité, rue, bâtiment** : vraiment supprimés, sauf s'ils sont utilisés (plaintes, bâtiments) ;
- **partie commune, catégorie, sous-catégorie, nuisance** : marquées supprimées (`SUPPR…`) ;
- une catégorie (ou sous-catégorie) refuse d'être supprimée tant qu'elle a des enfants actifs.

Ajout : champs obligatoires, longueurs de la base, code déjà pris, parent existant. `NUMCITE` et `CODERUE` ne sont **pas auto-incrémentés** (clé = plus grand numéro + 1) ; `IDBAT` l'est. La page ajoute et supprime **sans se recharger** (appels JavaScript vers `/GestionTables/AddElement` et `DeleteElement`).

---

## 5. Base de données

SQL Server, **sans migrations EF** : le modèle C# a été généré depuis une base existante. Toute évolution du schéma se fait **à la main** (script SQL), puis dans le code.

### Tables

`plainte`, `composer` (détails de plainte), `piece_jointe`, `utilisateur`, `cite`, `rue`, `cite_rue`, `batiment`, `batiment_rue`, `categorie`, `sous_categorie`, `nuisance`, `partie_commune`, `type_fait`, `nature_piece`, `etat`, `dico` (non utilisée par l'application).

### Script de création

`Scripts/architecture.sql` contient les `CREATE TABLE` (généré par SSMS, **encodé en UTF-16** : l'ouvrir avec un éditeur qui le gère). Il sert à recréer la structure sur une base vide. Les colonnes clés auto-incrémentées (`IDENTITY`) y figurent ; `composer.NUMDETAIL` en fait partie, ce qui est **indispensable** : sans lui, l'ajout d'un 2ᵉ détail de plainte échoue (clé en double).

Les **données de référence** (cités, rues, catégories…) ne sont pas dans ce script. L'état `Ec` (« en cours ») et le code `AUTR` (« Autre(s) », dans `type_fait` et `nature_piece`) sont écrits en dur dans le code : ils doivent exister dans la base.

### Compte applicatif

L'application n'a besoin que de **lire et écrire** les données : rôles `db_datareader` et `db_datawriter`, **pas** de droits de modification de schéma.

### Premier administrateur

Personne ne peut entrer sans être dans `utilisateur`. Pour amorcer :

```sql
INSERT INTO utilisateur (UserEmail, ACCESS)
VALUES ('prenom.nom@macon-habitat.com', 'S');
```

L'adresse doit être **identique** à celle envoyée par Microsoft. Ensuite, les autres utilisateurs s'ajoutent depuis la page « Gestion Utilisateurs » ; leur nom est rempli à leur première connexion.

### Sauvegarde

La base **et** le dossier des pièces forment un tout : une ligne de `piece_jointe` pointe vers un fichier. Ils se sauvegardent **ensemble** (même heure).

---

## 6. Configuration

.NET lit la configuration dans cet ordre (le dernier gagne) : `appsettings.json` → `appsettings.{Environnement}.json` → **variables d'environnement** → secrets utilisateur (développement uniquement). En conteneur, **tout se règle avec des variables d'environnement** ; le séparateur `:` devient `__` (`Storage:Racine` s'écrit `Storage__Racine`).

`appsettings.json` ne contient plus ni port d'écoute ni chaîne de connexion : ces éléments dépendent de l'environnement et sont fournis par `launchSettings.json` / les secrets en développement, par le conteneur en production.

| Clé | Variable d'environnement | Valeur dans l'image / exemple | Obligatoire | Rôle |
|---|---|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | *(aucune)* | **oui** | Connexion SQL Server. |
| `Storage:Racine` | `Storage__Racine` | `/data/pieces` | **oui** | Dossier des pièces. Vide : l'application refuse de démarrer. |
| `Storage:FichierTemoin` | `Storage__FichierTemoin` | `.montage_ok` | recommandé | Fichier qui doit exister dans la racine ; prouve que le volume est monté. |
| `Storage:MaxSize` | `Storage__MaxSize` | 20971520 (20 Mo) | non | Taille max d'un fichier. |
| `Storage:MaxTotalSize` | `Storage__MaxTotalSize` | 52428800 (50 Mo) | non | Total max des pièces d'une plainte. |
| `Storage:MaxPieces` | `Storage__MaxPieces` | 10 | non | Nombre max de pièces par plainte. |
| `Storage:Extensions` | `Storage__Extensions__0`, `__1`… | `.jpg .jpeg .png .pdf .docx .doc` | non | Extensions autorisées (un indice par valeur). |
| `Export:SofficePath` | `Export__SofficePath` | `soffice` | oui | Programme LibreOffice. Dans `appsettings.json`, c'est le chemin Windows (poste de développement). |
| `Export:TimeoutSeconds` | `Export__TimeoutSeconds` | 60 | non | Durée max d'une conversion. |
| `Export:ConversionsSimultanees` | `Export__ConversionsSimultanees` | 2 | non | Conversions en parallèle (≈ 150 Mo de mémoire et 1 cœur chacune). |
| `Export:DossierTemporaire` | `Export__DossierTemporaire` | `/data/tmp` | recommandé | Dossiers de travail et **profils LibreOffice**. |
| `ModelePlainte` | `ModelePlainte` | `Templates/TemplatePlainte.docx` | non | Chemin du modèle, relatif au dossier de l'application. |
| `Saml:IdpEntityId` | `Saml__IdpEntityId` | `https://sts.windows.net/<tenant>/` | **oui** | Identité du fournisseur (Entra ID). |
| `Saml:MetadataUrl` | `Saml__MetadataUrl` | URL de métadonnées Entra | **oui** | Chargée au démarrage : **le conteneur doit joindre `login.microsoftonline.com`**. |
| `Saml:SpEntityId` | `Saml__SpEntityId` | `https://<hôte public>/saml2` | **oui** | **Identifiant de l'application** (Service Provider) pour Entra ID : il doit être identique à l'« ID d'entité » déclaré dans Entra. C'est un nom, pas une adresse que Microsoft visite. |
| `Saml:CertificatPath` | `Saml__CertificatPath` | `/run/secrets/saml.pfx` | oui en production | Certificat (`.pfx` sans mot de passe). Absent : le fichier du dépôt est utilisé (développement seulement). |
| `DataProtection:Dossier` | `DataProtection__Dossier` | `/data/keys` | recommandé | Dossier des clés de chiffrement des sessions. Absent : clés non conservées. |
| `AllowedHosts` | `AllowedHosts` | `*` | non | À restreindre au nom public en production. |
| `ASPNETCORE_HTTP_PORTS` | idem | `8080` | non | Port d'écoute HTTP du conteneur. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | idem | `true` | non | Lit les en-têtes du reverse proxy (adresse et schéma publics). |
| `TZ` | idem | `Europe/Paris` | non | Fuseau horaire (l'application utilise l'heure locale). |
| `ASPNETCORE_ENVIRONMENT` | idem | `Production` (par défaut) | non | `Development` affiche la page d'erreur détaillée du développeur (au lieu de la page « Une erreur est survenue ») et charge `appsettings.Development.json` : **ne jamais l'utiliser en production**. |

**Secrets** (chaîne de connexion avec mot de passe, certificat) : jamais dans l'image ni dans git. Ils arrivent par le fichier `.env` (non versionné) et le dossier `secrets/` (non versionné).

---

## 7. Conteneurisation : ce qui a été mis en place

### 7.1 Objectif et choix retenus

Le point de départ était un `Dockerfile` généré par Visual Studio, bon pour le débogage mais incapable de faire tourner l'application : LibreOffice, le modèle Word et le certificat étaient absents, la configuration d'écoute ne convenait pas à un conteneur, et rien n'était prévu pour conserver les données. Les choix suivants ont été faits :

| Choix | Décision |
|---|---|
| Ce que contient l'image | le code, LibreOffice, les polices et le modèle Word, **sans aucune donnée ni secret** |
| Où vivent les données | base SQL Server **hors Docker** ; pièces, profils LibreOffice, clés de chiffrement et journaux sur **quatre volumes** |
| Configuration | **variables d'environnement** uniquement (`appsettings.json` ne contient plus ni port, ni chaîne de connexion) |
| Identité de l'application auprès de Microsoft | **Entity ID configurable** (`Saml:SpEntityId`), plus calculé depuis l'adresse d'écoute |
| Réseau | **HTTP sur 8080**, derrière un reverse proxy qui porte l'adresse publique et le HTTPS |
| Secrets | `.env` et `secrets/` hors de git ; le certificat est **monté à l'exécution**, jamais copié dans l'image |

### 7.2 Fichiers créés ou modifiés

| Fichier | Ce qui a été fait |
|---|---|
| `Plaintes/Dockerfile` | réécrit (voir §7.3) |
| `docker-compose.yml` | créé : service `plaintes`, quatre volumes, certificat monté, port, limite de mémoire ; service d'initialisation `init-pieces` |
| `.env.example` | créé : modèle du `.env` (non versionné) |
| `.dockerignore` | complété : exclut les fichiers `.pfx`, `appsettings.Development.json`, `secrets`, les journaux, les tests, `docs` et `Scripts` |
| `.gitignore` | `secrets/` et `*.pfx` ajoutés ; `.env` y était déjà |
| `Plaintes/Program.cs` | sortie console, dossier des clés, chemin du certificat configurable, Entity ID lu en configuration (§7.4) |
| `Plaintes/Plaintes.csproj` | modèle Word copié à la publication |
| `Plaintes/appsettings.json` | bloc `Kestrel`, chaîne de connexion et réglages de journalisation retirés |
| `Plaintes/Controllers/HomeController.cs`, `Views/Home/Error.cshtml` | page d'erreur `/Error` (§7.4) |
| `Plaintes/Sustainsys.Saml2.Tests2.pfx` | retiré du suivi git (`git rm --cached`) ; le fichier reste sur le poste |

### 7.3 Le Dockerfile

Quatre étapes, nommées comme dans le fichier généré par Visual Studio pour rester compatible avec son débogage en conteneur :

1. **`base`** (image `aspnet:10.0`) : installation de **LibreOffice** (`libreoffice-writer`), des polices équivalentes à celles de Word (`fonts-liberation` pour Arial, `fonts-crosextra-carlito` pour Calibri, `fonts-crosextra-caladea` pour Cambria), de `tzdata` et de `curl`. Réglages par défaut en `ENV` :
   - `TZ=Europe/Paris` (l'application utilise l'heure locale) ;
   - `ASPNETCORE_HTTP_PORTS=8080` ;
   - `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` ;
   - `Export__SofficePath=soffice`, `Export__DossierTemporaire=/data/tmp` ;
   - `Storage__Racine=/data/pieces`, `Storage__FichierTemoin=.montage_ok` ;
   - `DataProtection__Dossier=/data/keys`.
   Les dossiers `/data/pieces`, `/data/tmp`, `/data/keys` et `/app/Logs` sont créés et confiés à l'utilisateur `$APP_UID` (1654), sous lequel l'application tourne : **pas de `root`**.
2. **`build`** (image `sdk:10.0`) : copie du seul `.csproj` puis `dotnet restore`, puis copie du code. La restauration est ainsi réutilisée tant que les paquets ne changent pas.
3. **`publish`** : `dotnet publish` en `Release`.
4. **`final`** : repart de `base`, copie le résultat de `publish`, déclare le contrôle de santé (`curl http://localhost:8080/Account/Deconnecte` toutes les 30 s) et lance `dotnet Plaintes.dll`.

Le modèle Word n'est **pas** copié par une ligne du Dockerfile : une ligne `COPY Plaintes/Templates ./Templates` avait été ajoutée dans l'étape `build`, mais elle copiait vers `/src/Templates`, hors de la publication ; elle a été retirée. Le modèle arrive dans l'image grâce au `.csproj` (`CopyToPublishDirectory`), ce qui a été vérifié (`/app/Templates/TemplatePlainte.docx` présent).

### 7.4 Modifications du code nécessaires au conteneur

- **Journaux** : `Program.cs` écrit désormais aussi sur la **console** (`.WriteTo.Console()`), en plus du fichier : sans cela, `docker logs` restait vide.
- **Clés de chiffrement** : si `DataProtection:Dossier` est renseigné, les clés qui chiffrent le cookie de connexion et les jetons des formulaires y sont écrites (`PersistKeysToFileSystem`). Dans l'image, c'est `/data/keys`, un volume : un remplacement du conteneur ne déconnecte plus personne.
- **Certificat SAML** : le chemin est lu dans `Saml:CertificatPath` ; s'il est absent, le code garde l'ancien comportement (le fichier du dépôt), utile en développement.
- **Entity ID** : `Program.cs` lit `Saml:SpEntityId`. L'ancien code le déduisait de l'adresse d'écoute Kestrel, ce qui interdisait de s'identifier avec l'adresse publique derrière un proxy.
- **Modèle Word** : une ligne dans le `.csproj` le copie à la publication (`dotnet publish` ne l'embarquait pas).
- **Configuration** : le bloc `Kestrel` et la chaîne de connexion ont été retirés d'`appsettings.json` ; ports et chaîne arrivent par variables d'environnement.
- **Session et droits à jour** : `OnValidatePrincipal` (cookie) appelle `ComptesClaims.EtatSessionAsync` à chaque requête. Si l'utilisateur est supprimé ou inconnu, la session est rejetée et le cookie supprimé. Si son droit a changé en base, le claim `Role` du cookie est remplacé et le cookie renouvelé. Un compte déjà « refusé » n'est pas revérifié. Le résultat est gardé 10 secondes en mémoire (`AddMemoryCache`).
- **Page d'erreur** : `UseExceptionHandler` pointait vers `/Home/Error`, qui n'existait plus (page vide en production). Une action `Error` (`/Error`, accessible sans connexion) et une vue `Error.cshtml` aux couleurs du site ont été créées. Elles affichent « Une erreur est survenue » et le **numéro de requête**, identique au « Request id » des journaux, sans aucun détail technique. `UseExceptionHandler` pointe vers `/Error`.

### 7.5 Volumes

| Volume | Chemin dans le conteneur | Contenu |
|---|---|---|
| `pieces` | `/data/pieces` | pièces jointes |
| `tmp` | `/data/tmp` | profils LibreOffice et dossiers de travail des conversions |
| `keys` | `/data/keys` | clés de chiffrement des sessions |
| `logs` | `/app/Logs` | journaux (30 jours) |

Le dossier des pièces est protégé par un **fichier témoin** (`.montage_ok`) : l'application refuse de démarrer s'il est absent, pour ne pas écrire des pièces dans le conteneur si le volume n'est pas monté. Pour un volume neuf, le service `init-pieces` (profil `init` du compose) crée ce fichier :

```bash
docker compose --profile init run --rm init-pieces
```

Il s'exécute avec la **même image** que l'application, sous `root`, monte `pieces` sur `/data/pieces`, crée le fichier et donne le dossier à l'utilisateur 1654. Il n'est à lancer qu'une fois par volume.

### 7.6 Réseau, reverse proxy et connexion Microsoft

**Déploiement.** Le conteneur écoute en **HTTP sur le port 8080**. Le compose le publie sur `127.0.0.1:8080` seulement : il n'est joignable que depuis la machine, donc **par le reverse proxy**, jamais directement depuis le réseau. Le reverse proxy :
- porte l'adresse publique (`https://plaintes.macon-habitat.com`) et le certificat TLS ;
- transmet les requêtes à `http://127.0.0.1:8080` en conservant l'en-tête **`Host`** d'origine et en ajoutant `X-Forwarded-Proto: https` (avec nginx : `proxy_set_header Host $host;` et `proxy_set_header X-Forwarded-Proto $scheme;`) ;
- accepte des envois d'au moins ≈ 60 Mo (50 Mo de pièces par plainte ; nginx : `client_max_body_size 60m;`) et laisse un délai de réponse suffisant (un export lourd dure 10 à 30 s).

Le conteneur doit pouvoir **sortir** vers `login.microsoftonline.com` (métadonnées SAML, au démarrage) et joindre le serveur SQL.

**Ce qui a été constaté en simulant un proxy** (requêtes envoyées à la main au conteneur) :
- l'application reconstruit son adresse publique avec le **`Host`** et **`X-Forwarded-Proto`** ; **`X-Forwarded-Host` n'est pas lu**. Avec `Host: plaintes.exemple.fr` et `X-Forwarded-Proto: https`, la demande envoyée à Microsoft contenait l'adresse de retour `https://plaintes.exemple.fr/Saml2/Acs` ; avec seulement `X-Forwarded-Host`, elle contenait l'adresse interne du conteneur ;
- **`AllowedHosts`** est fixé par le compose à `${HOTE_PUBLIC};localhost`. Sans `localhost`, le contrôle de santé du conteneur (qui appelle `http://localhost:8080`) recevait **400** et le conteneur restait *unhealthy*. Un `Host` hors liste reçoit 400.

**Les trois adresses de la connexion Microsoft** (avec `HOTE_PUBLIC=plaintes.macon-habitat.com`) :

| Nom | Valeur | Origine |
|---|---|---|
| Entity ID | `https://plaintes.macon-habitat.com/saml2` | `Saml__SpEntityId`, composé par le compose ; doit être **identique** à l'« Identifiant » déclaré dans l'application Entra |
| URL de réponse (ACS) | `https://plaintes.macon-habitat.com/Saml2/Acs` | construite par l'application avec le `Host` et le schéma reçus du proxy ; doit figurer dans les URL de réponse d'Entra |
| Métadonnées | `Saml__MetadataUrl` | téléchargées au démarrage |

L'Entity ID et l'URL de réponse de production sont à faire déclarer par l'administrateur d'Entra (§8.1).

**Certificat SAML.** Monté en lecture seule sur `/run/secrets/saml.pfx` (compose : `./secrets/saml.pfx`), exclu de l'image par `.dockerignore`, retiré du suivi git. Le code n'accepte qu'un `.pfx` **sans mot de passe**.

**Essai en local (hors déploiement).** Les nouvelles adresses ne pouvant pas être déclarées dans Entra, un essai a été fait avec celles qui existaient déjà (`https://localhost:3001/saml2` et `https://localhost:3001/Saml2/Acs`) : le conteneur servait alors du HTTPS sur 3001 avec `secrets/saml.pfx` comme certificat de serveur, et le `.env` contenait `HOTE_PUBLIC=localhost:3001`. Ces réglages (`ASPNETCORE_HTTPS_PORTS`, `Kestrel__Certificates__Default__Path`, port publié 3001) ont ensuite été **retirés** du Dockerfile et du compose : le dépôt est en configuration de déploiement. Les incidents de cet essai figurent au §9.1.

### 7.7 Le fichier `docker-compose.yml`

Il décrit comment lancer l'application avec `docker compose up -d`. Les valeurs `${...}` viennent du fichier `.env`.

**Service `plaintes`** (l'application)

| Ligne | Rôle |
|---|---|
| `image: plaintes:${VERSION:-dev}` | l'image à lancer ; le numéro vient de `VERSION` |
| `build` | comment la fabriquer si elle n'existe pas (Dockerfile `Plaintes/Dockerfile`) |
| `restart: unless-stopped` | relance automatique après un plantage ou un redémarrage du serveur |
| `ports: 127.0.0.1:8080:8080` | port 8080, joignable **depuis le serveur seulement** (le reverse proxy s'y connecte) |
| `mem_limit: 1g` | mémoire maximale : 1 Go |
| `environment` | réglages de l'application, tirés du `.env` : base (`DB_*`), Microsoft (`SAML_*`), adresse publique (`HOTE_PUBLIC`) |
| `volumes` | les 4 volumes du §7.5 et le certificat `secrets/saml.pfx`, monté en lecture seule |

Avec `HOTE_PUBLIC=plaintes.macon-habitat.com`, l'application s'identifie auprès de Microsoft avec `https://plaintes.macon-habitat.com/saml2` et n'accepte que cette adresse (plus `localhost` pour le contrôle de santé).

**Service `init-pieces`** : crée une fois le fichier témoin d'un volume `pieces` neuf (§7.5). Il ne démarre pas avec `docker compose up`, seulement à la demande :

```bash
docker compose --profile init run --rm init-pieces
```

### 7.8 Essais réalisés le 08/10/2026 (Docker 29.8)

Faits sur le poste de développement, avec la configuration de l'essai local (HTTPS sur 3001, hôte `localhost:3001`, depuis retirée du dépôt) : le déploiement réel, en HTTP sur 8080 derrière un proxy, n'a pas été essayé de bout en bout.

**Vérifié**
- L'image se construit : ≈ 1,1 Go, dont l'essentiel est LibreOffice (24.2).
- Le processus tourne sous l'utilisateur `app` (1654). `TZ` vaut `Europe/Paris`. Le modèle Word est dans `/app/Templates`. Les polices de remplacement sont actives (`Calibri → Carlito`, `Arial → Liberation Sans`, `Cambria → Caladea`).
- LibreOffice convertit `TemplatePlainte.docx` en PDF dans le conteneur, avec cet utilisateur (l'avertissement `failed to launch javaldx` est sans conséquence).
- Sans fichier témoin, l'application refuse de démarrer avec le message prévu ; avec lui, elle démarre (« Stockage des pièces jointes opérationnel »).
- Le conteneur passe à l'état *healthy* ; `https://localhost:3001` répond, `/wwwroot` (logo, favicon, CSS, JS) est servi.
- Une page protégée redirige vers Microsoft avec l'Entity ID et l'adresse de retour attendus ; **la connexion Microsoft a abouti** (« authenticated » dans les journaux).
- Les journaux sortent sur la console **et** dans `/app/Logs` ; les clés sont écrites dans `/data/keys` et le **même fichier** est présent après `docker compose restart`.
- Une erreur réelle (réponse SAML invalide envoyée au conteneur) renvoie 500 avec la nouvelle page d'erreur, et l'exception est dans les journaux.

**Non vérifié**
- Le déploiement **HTTP sur 8080 derrière un vrai reverse proxy**, avec l'adresse publique.
- Les requêtes sur **SQL Server** depuis le conteneur : la base de l'essai (`(localdb)`) n'est pas joignable depuis Linux.
- Un **export PDF de bout en bout** d'un vrai dossier (seule la conversion du modèle a été essayée).
- Un vrai **reverse proxy**, un **partage réseau** à la place du volume `pieces`, le **débogage** avec le profil Visual Studio, et la publication sur un **registre**.

---

## 8. Déploiement et exploitation

### 8.1 Ce qu'il faut réunir avant le premier démarrage

1. **Une base SQL Server joignable depuis le conteneur**, créée avec `Scripts/architecture.sql`, contenant les données de référence, l'état `Ec`, les codes `AUTR` (`type_fait`, `nature_piece`) et au moins un utilisateur (§5). Compte SQL en lecture et écriture seulement.
2. **L'application SAML d'Entra ID**, avec l'Entity ID (`https://<hôte>/saml2`), l'URL de réponse (`https://<hôte>/Saml2/Acs`) et les revendications adresse e-mail et nom d'affichage. Ces déclarations sont faites par l'administrateur du tenant : **à lui demander pour l'adresse publique de production**.
3. **Un certificat** `.pfx` sans mot de passe dans `secrets/saml.pfx`, lisible par l'utilisateur 1654 (`chmod 644`). Exemple de création, non exécuté :
   ```bash
   openssl req -x509 -newkey rsa:2048 -nodes -keyout cle.pem -out cert.pem -days 1095 -subj "/CN=<hôte public>"
   openssl pkcs12 -export -out saml.pfx -inkey cle.pem -in cert.pem -passout pass:
   ```
4. **Un `.env`** copié depuis `.env.example`, avec `HOTE_PUBLIC` = l'adresse publique réelle (sans `https://`), et un compose qui publie le port **8080** (§7.7). `DB_SERVEUR` ne peut pas être un `(localdb)` : il faut un SQL Server accessible par le réseau (`host.docker.internal` pour une instance sur le poste de développement).
5. **Un volume `pieces` initialisé** (§7.5) et, en production, **un reverse proxy** conforme au §7.6.

Démarrage, tel qu'il a été fait lors des essais :

```bash
docker compose build
docker compose --profile init run --rm init-pieces     # une seule fois par volume
docker compose up -d
docker compose logs -f plaintes
```

### 8.2 Redéployer une nouvelle version

**Ce qui se trouve sur le serveur.** Une copie du dépôt git (code, `Plaintes/Dockerfile`, `docker-compose.yml`), le fichier `.env` et le dossier `secrets/` (le certificat). Ces deux derniers ne sont **pas dans git** : ils sont créés une fois sur le serveur et ne bougent pas lors d'une mise à jour. Les données (pièces, clés, journaux) sont dans les volumes Docker, et la base sur le serveur SQL.

**Comment le nouveau code arrive.** Le compose contient une section `build` : l'image est donc **construite sur le serveur** à partir du code récupéré avec git. C'est la méthode prévue et la seule qui ne demande aucune infrastructure en plus (voir plus bas pour les alternatives).

**Procédure (méthode prévue)**

1. **Sur le poste de développement** : les tests passent (`dotnet test`), le code est validé (commit) et envoyé (`git push`). Pour pouvoir revenir en arrière facilement, poser une étiquette git : `git tag v1.0.1` puis `git push --tags`.
2. **Si la version modifie la base** : appliquer d'abord le script SQL sur la base. Il doit rester compatible avec l'ancienne version, au cas où un retour arrière serait nécessaire.
3. **Sur le serveur**, dans le dossier du dépôt :
   ```bash
   git pull                                  # récupère le nouveau code
   # modifier VERSION dans .env (ex. VERSION=1.0.1)
   docker compose build                      # construit la nouvelle image (plaintes:1.0.1)
   docker compose up -d                      # remplace le conteneur par la nouvelle version
   ```
   Changer `VERSION` à chaque mise à jour garde l'**ancienne image** sur le serveur (`plaintes:1.0.0`), ce qui permet le retour arrière.
4. **Vérifier** : `docker compose ps` (état *healthy*), `docker compose logs --tail 50 plaintes` (« Stockage des pièces jointes opérationnel », pas d'erreur), puis se connecter et exporter un PDF.

La coupure dure quelques secondes : `up -d` arrête l'ancien conteneur et en démarre un nouveau (un seul conteneur, pas de mise à jour sans interruption). Les volumes ne sont pas touchés : pièces, clés de session et journaux sont conservés, les utilisateurs connectés ne sont pas déconnectés.

**Retour arrière**

1. Remettre l'ancienne valeur de `VERSION` dans `.env` (ex. `1.0.0`) et lancer `docker compose up -d`. L'ancienne image est encore sur le serveur.
2. Si elle a été supprimée : `git checkout v1.0.0`, `docker compose build`, `docker compose up -d`, puis `git checkout master` pour revenir à la branche.
3. Si un script SQL incompatible a été appliqué : il faut son script inverse, ou restaurer la sauvegarde (les saisies faites depuis sont perdues).

**Selon ce qui change**

| Ce qui change | Commandes sur le serveur |
|---|---|
| Code de l'application, `Dockerfile` | `git pull`, `VERSION` dans `.env`, `docker compose build`, `docker compose up -d` |
| Réglage du `.env` (adresse, mot de passe de la base…) ou `docker-compose.yml` | modifier le fichier, puis `docker compose up -d` (le conteneur est recréé, pas de reconstruction) |
| Certificat SAML renouvelé | remplacer `secrets/saml.pfx`, puis `docker compose restart plaintes` (et mettre à jour le certificat dans Entra) |
| Mises à jour de sécurité (Debian, .NET, LibreOffice), sans changement de code | `docker compose build --pull --no-cache` puis `docker compose up -d` |
| Base de données | script SQL sur la base, **avant** le déploiement de la version qui en dépend |

Pour récupérer de la place, supprimer les anciennes images devenues inutiles : `docker image prune` (celles qui ne sont plus utilisées par un conteneur).

**Autres façons d'amener l'image** (non mises en place, à choisir si le serveur n'a pas accès au dépôt git ou ne doit pas compiler) :
- **Registre d'images** : construire l'image ailleurs (poste ou outil d'intégration), la publier avec `docker push`, puis sur le serveur `docker compose pull` et `docker compose up -d`. Il faut un registre accessible et le nom de l'image dans le compose préfixé par ce registre (`image: <registre>/plaintes:${VERSION}`).
- **Fichier d'image** : `docker save plaintes:1.0.1 | gzip > plaintes-1.0.1.tar.gz` sur le poste, copie du fichier sur le serveur, puis `docker load < plaintes-1.0.1.tar.gz` et `docker compose up -d`.

*Ces procédures n'ont pas été essayées sur un serveur de production : la construction et le remplacement du conteneur ont été vérifiés en local (§7.8).*

### 8.3 Exploitation

**Ce qui a été mis en place pour surveiller.**
- Le contrôle de santé du conteneur (`docker compose ps`, état *healthy*), qui vérifie que le processus répond (pas que SQL Server est joignable).
- Les journaux sur la console (`docker compose logs plaintes`) et dans le volume `logs`.
- La page d'erreur, dont le numéro retrouve l'erreur : `docker compose logs plaintes | grep <numéro>`.

**Messages des journaux à connaître.**
- `Connexion refusée : aucun utilisateur actif pour l'identifiant …` : personne autorisé ou e-mail Microsoft différent de celui de la base.
- `La pièce … n'a pas pu être intégrée à l'export` : pièce ignorée dans un PDF.
- `Le fichier de la pièce jointe … est absent du stockage` : base et dossier des pièces incohérents.
- `Stockage des pièces jointes inutilisable` : arrêt volontaire au démarrage.

**À prévoir par l'exploitant.**
- **Sauvegardes** : base et volume `pieces` **ensemble**, même heure ; un test de restauration avec ouverture d'un dossier et export de son PDF.
- **Reconstruction** de l'image au moins une fois par mois avec `docker build --pull` pour intégrer les correctifs de Debian, .NET et LibreOffice ; `dotnet list package --vulnerable --include-transitive` pour les paquets NuGet.
- **Certificat SAML** : noter sa date d'expiration, le renouveler dans `secrets/` **et** dans Entra.
- **Métadonnées Microsoft** : lues au démarrage ; redémarrer l'application si la connexion échoue brusquement après une rotation de certificats Microsoft.
- **Utilisateurs** : « Supprimer » met la date de fin à **hier** : le compte est refusé immédiatement à la connexion suivante, et une session déjà ouverte est coupée en 10 secondes au plus (le cookie est supprimé).
- **Capacité** : un export coûte ≈ 3 s par document Word converti (≈ 6 s avec un profil LibreOffice neuf) ; deux conversions en parallèle, ≈ 150 Mo chacune.

---

## 9. Problèmes rencontrés et dépannage

### 9.1 Rencontrés pendant la mise en place et l'essai local

| Symptôme | Cause | Ce qui a été fait |
|---|---|---|
| L'image ne se construit pas : `SetApplicationName` introuvable | directive `using Microsoft.AspNetCore.DataProtection;` manquante (découvert seulement dans l'image) | directive ajoutée |
| Construction impossible après modification du Dockerfile | `Kestrel__Certificates__Default__Path: …` écrit avec `:` dans un `ENV` multi-lignes | remplacé par `=` |
| Le volume `pieces` contenait des sous-dossiers `keys`, `pieces`, `tmp` | `init-pieces` montait le volume sur `/data` entier ; Docker y copiait le contenu de l'image | le service monte `pieces` sur `/data/pieces` |
| Conteneur *unhealthy* en permanence | `AllowedHosts` ne contenait que l'hôte public, le contrôle de santé appelle `localhost` | `;localhost` ajouté dans le compose |
| Conteneur qui redémarre en boucle : « Fichier témoin '.montage_ok' introuvable » | volume `pieces` neuf, sans fichier témoin | `init-pieces` lancé une fois |
| Avertissements « variable is not set » au `docker compose up` | pas de fichier `.env` (seul `.env.example` existait) | `.env` créé à la racine du dépôt |
| `https://localhost:3001` inaccessible | le compose publiait 3001, mais le conteneur n'écoutait que sur 8080 en HTTP | HTTPS ajouté sur 3001 pour l'essai local, puis retiré (le déploiement utilise 8080) |
| Microsoft : `AADSTS700016` « Application with identifier 'https://localhost:8080/saml2' was not found » | `HOTE_PUBLIC=localhost:8080` donnait un Entity ID absent d'Entra | `HOTE_PUBLIC=localhost:3001`, identifiant déjà déclaré |
| Après la connexion Microsoft, page « localhost ne peut pas traiter cette demande » (HTTP 500) | `LocalDB is not supported on this platform` : `DB_SERVEUR=(localdb)\MSSQLLocalDB` | **non résolu** : il faut un SQL Server accessible par le réseau |
| Page d'erreur vide en production | `/Home/Error` n'existait plus | page `/Error` créée (§7.4) |
| Adresse de retour SAML interne au lieu de l'adresse publique | `X-Forwarded-Host` n'est pas lu | le proxy doit conserver le `Host` (§7.6) |

### 9.2 Autres symptômes connus

| Symptôme | Cause probable |
|---|---|
| Arrêt : « Le chemin de stockage (Storage:Racine) n'est pas configuré » | `Storage__Racine` vide (l'image le fixe à `/data/pieces`) |
| Arrêt : fichier `.pfx` introuvable | certificat non monté, ou `Saml__CertificatPath` faux |
| Toutes les pages répondent 400 | `Host` hors `AllowedHosts` (proxy qui le remplace, `HOTE_PUBLIC` mal écrit) |
| Connexion Microsoft en boucle, ou « adresse de réponse non valide » | URL de réponse absente d'Entra, ou `Host` / `X-Forwarded-Proto` non transmis |
| Connexion réussie mais « accès refusé » | e-mail absent de `utilisateur`, `DATEFINU` renseignée, ou e-mail différent de celui de Microsoft (journal `Connexion refusée…`) |
| Tout le monde déconnecté après un remplacement, ou erreur 400 à l'envoi d'un formulaire | volume `keys` absent ou `DataProtection__Dossier` vide |
| Envoi de pièces refusé (413 ou coupure) | limite du reverse proxy inférieure à 60 Mo |
| Export PDF : « LibreOffice est introuvable » | `Export__SofficePath` remplacé par un chemin Windows |
| Export PDF lent la première fois | profils LibreOffice recréés (volume `tmp` absent) : ≈ 3 s de plus, une fois |
| Export PDF : mise en page différente de Word | police du modèle absente de l'image |
| Export PDF : pièce Word absente | conversion échouée ou délai dépassé (`Export__TimeoutSeconds`) |
| Export PDF : « Le fichier de cette pièce est introuvable » | pièce en base mais fichier absent du volume |
| Heures décalées | fuseau en UTC (l'image fixe `Europe/Paris`) |
| Aucun log fichier | `/app/Logs` non accessible en écriture (droits du volume, utilisateur 1654) |
| Déconnexion : Microsoft affiche `AADSTS50070` (« Signout failed … session indexes … did not match ») | le cookie de l'application vit plus longtemps que la session Microsoft ; l'index de session envoyé à la déconnexion est périmé | l'index n'est plus conservé dans le cookie (`Program.cs`) : Microsoft ferme la session en cours sans le vérifier |

---

## 10. Développement local et tests

**Environnement de développement.** SDK .NET 10, SQL Server (LocalDB), LibreOffice pour tester l'export réel, Visual Studio. Les ports viennent de `Properties/launchSettings.json` (`http://localhost:5101`, `https://localhost:7075`), la chaîne de connexion des secrets utilisateur, les pièces de `appsettings.Development.json` (dossier et fichier témoin à créer).

```bash
dotnet build Plaintes.slnx
dotnet test Plaintes.Tests
cd Plaintes
dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\MSSQLLocalDB;Database=PLAINTES;User Id=...;Password=...;TrustServerCertificate=True"
dotnet user-secrets set "Saml:CertificatPath" "C:\chemin\vers\dev.pfx"
```

Le certificat du dépôt n'étant plus suivi par git, **`Saml:CertificatPath` doit être défini en développement** (le fichier local reste sur le poste).

**Tests.** 323 tests xUnit, tous réussis au 08/10/2026. Les tests de contrôleurs et de services utilisent une base EF Core en mémoire ; cinq tests utilisent un **vrai LibreOffice** et sont ignorés s'il est absent ; un test construit le modèle EF avec le fournisseur SQL Server pour détecter les erreurs de mapping. Ajouts et ajustements récents : gestion des tables et des utilisateurs, page « accès refusé », test de l'action `Error`, test de `DeleteUser` aligné sur la date de fin « hier », vérification des sessions (utilisateur actif, supprimé, inconnu, compte refusé, résultat gardé en mémoire, droit mis à jour).

**Zone de test retirée.** Les pages affichaient en développement une zone de boutons de cas de test. Elle a été supprimée : composant `_ZoneTest`, modèle `CasTest`, appels dans les vues et script de cas de la page « Gestion des tables ».

**Pas d'intégration continue** : les tests ne tournent que sur demande, et l'image n'est construite que manuellement.

---

## 11. Dette technique : corrigé et restant

### 11.1 Corrigé

- Image Docker complète (LibreOffice, polices, modèle Word, utilisateur non administrateur), compose, `.env.example`, `.dockerignore`.
- `docker logs` renseigné (sortie console) ; clés de chiffrement persistées.
- Entity ID SAML configurable (`Saml:SpEntityId`) ; chemin du certificat configurable (`Saml:CertificatPath`).
- Certificat SAML retiré du suivi git (`git rm --cached`) et `*.pfx` ignoré.
- Page d'erreur de production créée.
- Menu : lien « Gestion des tables » corrigé (`/GestionTables`) et barre active qui suit la page.
- Zone de test supprimée ; code mort retiré (`ErrorViewModel`, `CasTest`).
- Suppression d'un utilisateur : date de fin à **hier** (refus immédiat).
- **Session et droits à jour** : vérifiés à chaque requête ; une suppression coupe la session (cookie supprimé) et un changement de droit est appliqué sans reconnexion, au plus 10 secondes après la modification en base.
- Script de création complet des tables : `Scripts/architecture.sql`.

### 11.2 Restant, par ordre d'importance

1. **Certificat de production** : en créer un dédié et le déclarer côté Entra si nécessaire. L'ancien certificat « Tests2 » reste dans l'**historique git** : à considérer comme connu de tous ceux qui ont accès au dépôt.
2. **Mot de passe en clair dans le code** : `AppDbContext.OnConfiguring` contient une chaîne de connexion de secours avec un mot de passe (`#warning`), utilisée seulement si aucune option n'est fournie ; à supprimer.
3. **Suppression d'un dossier accessible en GET** : `/Dossiers/{id}/delete` n'est restreinte à aucun verbe ni protégée par le jeton anti-falsification : un lien piégé peut supprimer un dossier. À passer en `POST` + `[ValidateAntiForgeryToken]`.
4. **Base joignable depuis le conteneur** : non vérifiée (voir §7.8 et §9.1).
5. **`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`** : fait confiance à tout appelant ; sans danger tant que le port n'est joignable que par le proxy (c'est le cas dans le compose).
6. **Base** : pas de migrations ; pas de point de santé qui teste la base.
7. **Image lourde** (≈ 1,1 Go) : LibreOffice, paquets de développement dans la publication (génération de code, outils EF) et binaires de toutes les plateformes (`runtimes/`).
8. **`AllowedHosts`** : sans valeur dans `appsettings.json` désormais (donc ouvert) ; le compose le restreint en déploiement.
9. **Empreinte SHA-256 des pièces** enregistrée mais jamais vérifiée.
10. **Pas d'intégration continue** ni de construction automatique d'image.
11. **Code inutilisé** : `Saml:LogoutUrl` dans `appsettings.json`, la variable `url` dans `Program.cs`, la table `dico` et son entité.

---

## 12. Annexes

### Adresses de l'application

| Adresse | Accès | Rôle |
|---|---|---|
| `/` | connecté | redirige vers `/Dossiers` |
| `/Dossiers` | connecté | liste (paramètres : filtres, `tri`, `sens`, `page`, `pageSize`) |
| `/Dossiers/Creer` | connecté | saisie (GET/POST) |
| `/Dossiers/Creer/Rues?numCite=` | connecté | adresses d'une cité (JSON) |
| `/Dossiers/{id}/update` | connecté | modification (GET/POST) |
| `/Dossiers/{id}/delete` | connecté | suppression ⚠ (voir §11.2) |
| `/Dossiers/{id}/download-pdf` | connecté | export PDF |
| `/Dossiers/PiecesJointes/{id}` | connecté | ouvre ou télécharge une pièce |
| `/Statistiques` | connecté | statistiques (paramètre : `annee`) |
| `/Account/AllAccounts` | A, S | gestion des utilisateurs |
| `/Account/AddAccount`, `EditAccount`, `DeleteAccount` | A, S (POST) | actions sur les utilisateurs |
| `/Account/Login`, `Logout`, `Deconnecte` | public | connexion, déconnexion |
| `/Account/AccesRefuse`, `AccessDenied` | public | page « accès refusé » (403) |
| `/GestionTables` | A, S | gestion des tables |
| `/GestionTables/AddElement`, `DeleteElement` | A, S (POST) | ajout, suppression |
| `/Error` | public | page d'erreur (appelée par l'application en cas d'exception) |
| `/Saml2/...` | public | module SAML (retour de connexion `/Saml2/Acs`) |

### Commandes Docker utilisées

```bash
docker compose build                                  # construire l'image
docker compose --profile init run --rm init-pieces    # fichier témoin d'un volume neuf
docker compose up -d                                  # (re)lancer
docker compose ps                                     # état et santé
docker compose logs -f plaintes                       # journaux en direct
docker compose exec plaintes soffice --version        # LibreOffice présent ?
```
