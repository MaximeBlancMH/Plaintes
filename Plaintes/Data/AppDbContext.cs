using Microsoft.EntityFrameworkCore;
using Plaintes.Models;

namespace Plaintes.Data
{
    public partial class AppDbContext : DbContext
    {

        public AppDbContext()
        { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        { }

        public virtual DbSet<Batiment> Batiments { get; set; }

        public virtual DbSet<Categorie> Categories { get; set; }

        public virtual DbSet<Cite> Cites { get; set; }

        public virtual DbSet<Composer> Composers { get; set; }

        public virtual DbSet<Dico> Dicos { get; set; }

        public virtual DbSet<Entree> Entrees { get; set; }

        public virtual DbSet<Etat> Etats { get; set; }

        public virtual DbSet<Nuisance> Nuisances { get; set; }

        public virtual DbSet<PartieCommune> PartieCommunes { get; set; }

        public virtual DbSet<PieceJointe> PieceJointes { get; set; }

        public virtual DbSet<Plainte> Plaintes { get; set; }

        public virtual DbSet<Rue> Rues { get; set; }

        public virtual DbSet<SousCategorie> SousCategories { get; set; }

        public virtual DbSet<Tranche> Tranches { get; set; }

        public virtual DbSet<Utilisateur> Utilisateurs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
            => optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PLAINTES;User Id=mblanc;Password=password;TrustServerCertificate=True");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Batiment>(entity =>
            {
                entity.HasKey(e => e.IdBat).HasName("PK_Batiment");

                entity.ToTable("batiment");

                entity.HasIndex(e => new { e.IdTranche, e.NumBat }, "UQ_Batiment_Tranche_NumBat").IsUnique();

                entity.Property(e => e.IdBat).HasColumnName("IDBAT");
                entity.Property(e => e.IdTranche).HasColumnName("IDTRANCHE");
                entity.Property(e => e.LibBat)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBBAT");
                entity.Property(e => e.NumBat)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMBAT");
                entity.Property(e => e.NumCite).HasColumnName("NUMCITE");

                entity.HasOne(d => d.IdTrancheNavigation).WithMany(p => p.Batiments)
                    .HasForeignKey(d => d.IdTranche)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Batiment_Tranche");

                entity.HasOne(d => d.NumCiteNavigation).WithMany(p => p.Batiments)
                    .HasForeignKey(d => d.NumCite)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Batiment_Cite");
            });

            modelBuilder.Entity<Categorie>(entity =>
            {
                entity.HasKey(e => e.CodeCat).HasName("PK_Categorie");

                entity.ToTable("categorie");

                entity.Property(e => e.CodeCat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.LibCat)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBCAT");
                entity.Property(e => e.SupprCat).HasColumnName("SUPPRCAT");
            });

            modelBuilder.Entity<Cite>(entity =>
            {
                entity.HasKey(e => e.NumCite).HasName("PK_Cite");

                entity.ToTable("cite");

                entity.Property(e => e.NumCite)
                    .ValueGeneratedNever()
                    .HasColumnName("NUMCITE");
                entity.Property(e => e.LibCite)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBCITE");
                entity.Property(e => e.Secteur)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("SECTEUR");
            });

            modelBuilder.Entity<Composer>(entity =>
            {
                entity.HasKey(e => e.NumDetail).HasName("PK_Composer");

                entity.ToTable("composer");

                entity.Property(e => e.NumDetail)
                    .ValueGeneratedNever()
                    .HasColumnName("NUMDETAIL");
                entity.Property(e => e.CodeNuisance)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODENUISANCE");
                entity.Property(e => e.Cout).HasColumnName("COUT");
                entity.Property(e => e.NumPlainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.SupprDet).HasColumnName("SUPPRDET");

                entity.HasOne(d => d.CodeNuisanceNavigation).WithMany(p => p.Composers)
                    .HasForeignKey(d => d.CodeNuisance)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Composer_Nuisance");

                entity.HasOne(d => d.NumPlainteNavigation).WithMany(p => p.Composers)
                    .HasForeignKey(d => d.NumPlainte)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Composer_Plainte");
            });

            modelBuilder.Entity<Dico>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_Dico");

                entity.ToTable("dico");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Champs)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("champs");
                entity.Property(e => e.Description)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("description");
                entity.Property(e => e.NomTable)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("nomtable");
            });

            modelBuilder.Entity<Entree>(entity =>
            {
                entity.HasKey(e => e.IdEntree).HasName("PK_Entree");

                entity.ToTable("entree");

                entity.HasIndex(e => new { e.IdBat, e.NumEntree }, "UQ_Entree_Bat_NumEntree").IsUnique();

                entity.Property(e => e.IdEntree).HasColumnName("IDENTREE");
                entity.Property(e => e.CodeRue).HasColumnName("CODERUE");
                entity.Property(e => e.IdBat).HasColumnName("IDBAT");
                entity.Property(e => e.NumEntree)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMENTREE");
                entity.Property(e => e.NumRue)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NUMRUE");

                entity.HasOne(d => d.CodeRueNavigation).WithMany(p => p.Entrees)
                    .HasForeignKey(d => d.CodeRue)
                    .HasConstraintName("FK_Entree_Rue");

                entity.HasOne(d => d.IdBatNavigation).WithMany(p => p.Entrees)
                    .HasForeignKey(d => d.IdBat)
                    .HasConstraintName("FK_Entree_Batiment");
            });

            modelBuilder.Entity<Etat>(entity =>
            {
                entity.HasKey(e => e.CodEtat).HasName("PK_Etat");

                entity.ToTable("etat");

                entity.HasIndex(e => e.LibEtat, "UQ_Etat_Libelle").IsUnique();

                entity.Property(e => e.CodEtat)
                    .HasMaxLength(2)
                    .IsUnicode(false)
                    .HasColumnName("CODETAT");
                entity.Property(e => e.LibEtat)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBETAT");
            });

            modelBuilder.Entity<Nuisance>(entity =>
            {
                entity.HasKey(e => e.CodeNuisance).HasName("PK_Nuisance");

                entity.ToTable("nuisance");

                entity.Property(e => e.CodeNuisance)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODENUISANCE");
                entity.Property(e => e.CodeSousCat)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODESOUSCAT");
                entity.Property(e => e.LibNuisance)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBNUISANCE");
                entity.Property(e => e.SupprNuisance).HasColumnName("SUPPRNUISANCE");

                entity.HasOne(d => d.CodeSousCatNavigation).WithMany(p => p.Nuisances)
                    .HasForeignKey(d => d.CodeSousCat)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Nuisance_SousCategorie");
            });

            modelBuilder.Entity<PartieCommune>(entity =>
            {
                entity.HasKey(e => e.CodePartieCommune).HasName("PK_PartieCommune");

                entity.ToTable("partie_commune");

                entity.Property(e => e.CodePartieCommune)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODEPARTIECOMMUNE");
                entity.Property(e => e.LibPartieCommune)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBPARTIECOMMUNE");
                entity.Property(e => e.SupprPc).HasColumnName("SUPPRPC");
            });

            modelBuilder.Entity<PieceJointe>(entity =>
            {
                entity.HasKey(e => e.NumPiece).HasName("PK_PieceJointe");

                entity.ToTable("piece_jointe");

                entity.Property(e => e.NumPiece).HasColumnName("NUMPIECE");
                entity.Property(e => e.Fichier).HasColumnName("FICHIER");
                entity.Property(e => e.LibPiece)
                    .HasMaxLength(150)
                    .IsUnicode(false)
                    .HasColumnName("LIBPIECE");
                entity.Property(e => e.NumPlainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.SupprPj).HasColumnName("SUPPRPJ");
                entity.Property(e => e.TaillePiece)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("TAILLEPIECE");
                entity.Property(e => e.TypePiece)
                    .HasMaxLength(90)
                    .IsUnicode(false)
                    .HasColumnName("TYPEPIECE");

                entity.HasOne(d => d.NumPlainteNavigation).WithMany(p => p.PieceJointes)
                    .HasForeignKey(d => d.NumPlainte)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PieceJointe_Plainte");
            });

            modelBuilder.Entity<Plainte>(entity =>
            {
                entity.HasKey(e => e.NumPlainte).HasName("PK_Plainte");

                entity.ToTable("plainte");

                entity.Property(e => e.NumPlainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.CodeCat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.CodePartieCommune)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODEPARTIECOMMUNE");
                entity.Property(e => e.CodeRue).HasColumnName("CODERUE");
                entity.Property(e => e.CodEtat)
                    .HasMaxLength(2)
                    .IsUnicode(false)
                    .HasColumnName("CODETAT");
                entity.Property(e => e.IdUser)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("IdUser");
                entity.Property(e => e.Commentaire)
                    .HasMaxLength(250)
                    .IsUnicode(false)
                    .HasColumnName("COMMENTAIRE");
                entity.Property(e => e.DateDeb).HasColumnName("DATEDEB");
                entity.Property(e => e.DateFin).HasColumnName("DATEFIN");
                entity.Property(e => e.NumCite).HasColumnName("NUMCITE");
                entity.Property(e => e.NumEntree)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMENTREE");
                entity.Property(e => e.NumReclamation)
                    .HasMaxLength(12)
                    .IsUnicode(false)
                    .HasColumnName("NUMRECLAMATION");
                entity.Property(e => e.NumRue)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NUMRUE");
                entity.Property(e => e.Suppr).HasColumnName("SUPPR");
                entity.Property(e => e.DateCreation).HasColumnName("DATECREATION");
                entity.Property(e => e.DateModification).HasColumnName("DATEMODIF");

                entity.HasOne(d => d.CodeCatNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.CodeCat)
                    .HasConstraintName("FK_Plainte_Categorie");

                entity.HasOne(d => d.CodePartieCommuneNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.CodePartieCommune)
                    .HasConstraintName("FK_Plainte_PartieCommune");

                entity.HasOne(d => d.CodeRueNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.CodeRue)
                    .HasConstraintName("FK_Plainte_Rue");

                entity.HasOne(d => d.CodEtatNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.CodEtat)
                    .HasConstraintName("FK_Plainte_Etat");

                entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.IdUser)
                    .HasConstraintName("FK_Plainte_Utilisateur");

                entity.HasOne(d => d.NumCiteNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.NumCite)
                    .HasConstraintName("FK_Plainte_Cite");
            });

            modelBuilder.Entity<Rue>(entity =>
            {
                entity.HasKey(e => e.CodeRue).HasName("PK_Rue");

                entity.ToTable("rue");

                entity.Property(e => e.CodeRue)
                    .ValueGeneratedNever()
                    .HasColumnName("CODERUE");
                entity.Property(e => e.Cp)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("CP");
                entity.Property(e => e.LibRue)
                    .HasMaxLength(150)
                    .IsUnicode(false)
                    .HasColumnName("LIBRUE");
                entity.Property(e => e.Ville)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("VILLE");
            });

            modelBuilder.Entity<SousCategorie>(entity =>
            {
                entity.HasKey(e => e.CodeSousCat).HasName("PK_SousCategorie");

                entity.ToTable("sous_categorie");

                entity.Property(e => e.CodeSousCat)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODESOUSCAT");
                entity.Property(e => e.CodeCat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.LibSousCat)
                    .HasMaxLength(150)
                    .IsUnicode(false)
                    .HasColumnName("LIBSOUSCAT");
                entity.Property(e => e.SupprSousCat).HasColumnName("SUPPRSOUSCAT");

                entity.HasOne(d => d.CodeCatNavigation).WithMany(p => p.SousCategories)
                    .HasForeignKey(d => d.CodeCat)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SousCategorie_Categorie");
            });

            modelBuilder.Entity<Tranche>(entity =>
            {
                entity.HasKey(e => e.IdTranche).HasName("PK_Tranche");

                entity.ToTable("tranche");

                entity.HasIndex(e => new { e.NumCite, e.CodeTranche }, "UQ_Tranche_Cite_Code").IsUnique();

                entity.Property(e => e.IdTranche).HasColumnName("IDTRANCHE");
                entity.Property(e => e.CodeTranche)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODETRANCHE");
                entity.Property(e => e.LibTranche)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBTRANCHE");
                entity.Property(e => e.NumCite).HasColumnName("NUMCITE");

                entity.HasOne(d => d.NumCiteNavigation).WithMany(p => p.Tranches)
                    .HasForeignKey(d => d.NumCite)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Tranche_Cite");
            });

            modelBuilder.Entity<Utilisateur>(entity =>
            {
                entity.HasKey(e => e.IdUser).HasName("PK_Utilisateur");

                entity.ToTable("utilisateur");

                entity.Property(e => e.IdUser)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("IdUser");
                entity.Property(e => e.Access)
                    .HasMaxLength(1)
                    .IsUnicode(false)
                    .HasColumnName("ACCESS");
                entity.Property(e => e.DateFinu).HasColumnName("DATEFINU");
                entity.Property(e => e.NomUser)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NOMUSER");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    }
}
