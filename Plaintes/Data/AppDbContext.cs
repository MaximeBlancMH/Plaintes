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
                entity.HasKey(e => e.Idbat).HasName("PK_Batiment");

                entity.ToTable("batiment");

                entity.HasIndex(e => new { e.Idtranche, e.Numbat }, "UQ_Batiment_Tranche_NumBat").IsUnique();

                entity.Property(e => e.Idbat).HasColumnName("IDBAT");
                entity.Property(e => e.Idtranche).HasColumnName("IDTRANCHE");
                entity.Property(e => e.Libbat)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBBAT");
                entity.Property(e => e.Numbat)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMBAT");
                entity.Property(e => e.Numcite).HasColumnName("NUMCITE");

                entity.HasOne(d => d.IdtrancheNavigation).WithMany(p => p.Batiments)
                    .HasForeignKey(d => d.Idtranche)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Batiment_Tranche");

                entity.HasOne(d => d.NumciteNavigation).WithMany(p => p.Batiments)
                    .HasForeignKey(d => d.Numcite)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Batiment_Cite");
            });

            modelBuilder.Entity<Categorie>(entity =>
            {
                entity.HasKey(e => e.Codecat).HasName("PK_Categorie");

                entity.ToTable("categorie");

                entity.Property(e => e.Codecat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.Libcat)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBCAT");
                entity.Property(e => e.Supprcat).HasColumnName("SUPPRCAT");
            });

            modelBuilder.Entity<Cite>(entity =>
            {
                entity.HasKey(e => e.Numcite).HasName("PK_Cite");

                entity.ToTable("cite");

                entity.Property(e => e.Numcite)
                    .ValueGeneratedNever()
                    .HasColumnName("NUMCITE");
                entity.Property(e => e.Libcite)
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
                entity.HasKey(e => e.Numdetail).HasName("PK_Composer");

                entity.ToTable("composer");

                entity.Property(e => e.Numdetail)
                    .ValueGeneratedNever()
                    .HasColumnName("NUMDETAIL");
                entity.Property(e => e.Codenuisance)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODENUISANCE");
                entity.Property(e => e.Cout).HasColumnName("COUT");
                entity.Property(e => e.Numplainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.Supprdet).HasColumnName("SUPPRDET");

                entity.HasOne(d => d.CodenuisanceNavigation).WithMany(p => p.Composers)
                    .HasForeignKey(d => d.Codenuisance)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Composer_Nuisance");

                entity.HasOne(d => d.NumplainteNavigation).WithMany(p => p.Composers)
                    .HasForeignKey(d => d.Numplainte)
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
                entity.Property(e => e.Nomtable)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("nomtable");
            });

            modelBuilder.Entity<Entree>(entity =>
            {
                entity.HasKey(e => e.Identree).HasName("PK_Entree");

                entity.ToTable("entree");

                entity.HasIndex(e => new { e.Idbat, e.Numentree }, "UQ_Entree_Bat_NumEntree").IsUnique();

                entity.Property(e => e.Identree).HasColumnName("IDENTREE");
                entity.Property(e => e.Coderue).HasColumnName("CODERUE");
                entity.Property(e => e.Idbat).HasColumnName("IDBAT");
                entity.Property(e => e.Numentree)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMENTREE");
                entity.Property(e => e.Numrue)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NUMRUE");

                entity.HasOne(d => d.CoderueNavigation).WithMany(p => p.Entrees)
                    .HasForeignKey(d => d.Coderue)
                    .HasConstraintName("FK_Entree_Rue");

                entity.HasOne(d => d.IdbatNavigation).WithMany(p => p.Entrees)
                    .HasForeignKey(d => d.Idbat)
                    .HasConstraintName("FK_Entree_Batiment");
            });

            modelBuilder.Entity<Etat>(entity =>
            {
                entity.HasKey(e => e.Codetat).HasName("PK_Etat");

                entity.ToTable("etat");

                entity.HasIndex(e => e.Libetat, "UQ_Etat_Libelle").IsUnique();

                entity.Property(e => e.Codetat)
                    .HasMaxLength(2)
                    .IsUnicode(false)
                    .HasColumnName("CODETAT");
                entity.Property(e => e.Libetat)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBETAT");
            });

            modelBuilder.Entity<Nuisance>(entity =>
            {
                entity.HasKey(e => e.Codenuisance).HasName("PK_Nuisance");

                entity.ToTable("nuisance");

                entity.Property(e => e.Codenuisance)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODENUISANCE");
                entity.Property(e => e.Codesouscat)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODESOUSCAT");
                entity.Property(e => e.Libnuisance)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBNUISANCE");
                entity.Property(e => e.Supprnuisance).HasColumnName("SUPPRNUISANCE");

                entity.HasOne(d => d.CodesouscatNavigation).WithMany(p => p.Nuisances)
                    .HasForeignKey(d => d.Codesouscat)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Nuisance_SousCategorie");
            });

            modelBuilder.Entity<PartieCommune>(entity =>
            {
                entity.HasKey(e => e.Codepartiecommune).HasName("PK_PartieCommune");

                entity.ToTable("partie_commune");

                entity.Property(e => e.Codepartiecommune)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODEPARTIECOMMUNE");
                entity.Property(e => e.Libpartiecommune)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("LIBPARTIECOMMUNE");
                entity.Property(e => e.Supprpc).HasColumnName("SUPPRPC");
            });

            modelBuilder.Entity<PieceJointe>(entity =>
            {
                entity.HasKey(e => e.Numpiece).HasName("PK_PieceJointe");

                entity.ToTable("piece_jointe");

                entity.Property(e => e.Numpiece).HasColumnName("NUMPIECE");
                entity.Property(e => e.Fichier).HasColumnName("FICHIER");
                entity.Property(e => e.Libpiece)
                    .HasMaxLength(150)
                    .IsUnicode(false)
                    .HasColumnName("LIBPIECE");
                entity.Property(e => e.Numplainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.Supprpj).HasColumnName("SUPPRPJ");
                entity.Property(e => e.Taillepiece)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("TAILLEPIECE");
                entity.Property(e => e.Typepiece)
                    .HasMaxLength(90)
                    .IsUnicode(false)
                    .HasColumnName("TYPEPIECE");

                entity.HasOne(d => d.NumplainteNavigation).WithMany(p => p.PieceJointes)
                    .HasForeignKey(d => d.Numplainte)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PieceJointe_Plainte");
            });

            modelBuilder.Entity<Plainte>(entity =>
            {
                entity.HasKey(e => e.Numplainte).HasName("PK_Plainte");

                entity.ToTable("plainte");

                entity.Property(e => e.Numplainte).HasColumnName("NUMPLAINTE");
                entity.Property(e => e.Codecat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.Codepartiecommune)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODEPARTIECOMMUNE");
                entity.Property(e => e.Coderue).HasColumnName("CODERUE");
                entity.Property(e => e.Codetat)
                    .HasMaxLength(2)
                    .IsUnicode(false)
                    .HasColumnName("CODETAT");
                entity.Property(e => e.Codeuser)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("CODEUSER");
                entity.Property(e => e.Commentaire)
                    .HasMaxLength(250)
                    .IsUnicode(false)
                    .HasColumnName("COMMENTAIRE");
                entity.Property(e => e.Datedeb).HasColumnName("DATEDEB");
                entity.Property(e => e.Datefin).HasColumnName("DATEFIN");
                entity.Property(e => e.Numcite).HasColumnName("NUMCITE");
                entity.Property(e => e.Numentree)
                    .HasMaxLength(11)
                    .IsUnicode(false)
                    .HasColumnName("NUMENTREE");
                entity.Property(e => e.Numreclamation)
                    .HasMaxLength(12)
                    .IsUnicode(false)
                    .HasColumnName("NUMRECLAMATION");
                entity.Property(e => e.Numrue)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NUMRUE");
                entity.Property(e => e.Suppr).HasColumnName("SUPPR");

                entity.HasOne(d => d.CodecatNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Codecat)
                    .HasConstraintName("FK_Plainte_Categorie");

                entity.HasOne(d => d.CodepartiecommuneNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Codepartiecommune)
                    .HasConstraintName("FK_Plainte_PartieCommune");

                entity.HasOne(d => d.CoderueNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Coderue)
                    .HasConstraintName("FK_Plainte_Rue");

                entity.HasOne(d => d.CodetatNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Codetat)
                    .HasConstraintName("FK_Plainte_Etat");

                entity.HasOne(d => d.CodeuserNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Codeuser)
                    .HasConstraintName("FK_Plainte_Utilisateur");

                entity.HasOne(d => d.NumciteNavigation).WithMany(p => p.Plaintes)
                    .HasForeignKey(d => d.Numcite)
                    .HasConstraintName("FK_Plainte_Cite");
            });

            modelBuilder.Entity<Rue>(entity =>
            {
                entity.HasKey(e => e.Coderue).HasName("PK_Rue");

                entity.ToTable("rue");

                entity.Property(e => e.Coderue)
                    .ValueGeneratedNever()
                    .HasColumnName("CODERUE");
                entity.Property(e => e.Cp)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("CP");
                entity.Property(e => e.Librue)
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
                entity.HasKey(e => e.Codesouscat).HasName("PK_SousCategorie");

                entity.ToTable("sous_categorie");

                entity.Property(e => e.Codesouscat)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODESOUSCAT");
                entity.Property(e => e.Codecat)
                    .HasMaxLength(4)
                    .IsUnicode(false)
                    .HasColumnName("CODECAT");
                entity.Property(e => e.Libsouscat)
                    .HasMaxLength(150)
                    .IsUnicode(false)
                    .HasColumnName("LIBSOUSCAT");
                entity.Property(e => e.Supprsouscat).HasColumnName("SUPPRSOUSCAT");

                entity.HasOne(d => d.CodecatNavigation).WithMany(p => p.SousCategories)
                    .HasForeignKey(d => d.Codecat)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SousCategorie_Categorie");
            });

            modelBuilder.Entity<Tranche>(entity =>
            {
                entity.HasKey(e => e.Idtranche).HasName("PK_Tranche");

                entity.ToTable("tranche");

                entity.HasIndex(e => new { e.Numcite, e.Codetranche }, "UQ_Tranche_Cite_Code").IsUnique();

                entity.Property(e => e.Idtranche).HasColumnName("IDTRANCHE");
                entity.Property(e => e.Codetranche)
                    .HasMaxLength(5)
                    .IsUnicode(false)
                    .HasColumnName("CODETRANCHE");
                entity.Property(e => e.Libtranche)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LIBTRANCHE");
                entity.Property(e => e.Numcite).HasColumnName("NUMCITE");

                entity.HasOne(d => d.NumciteNavigation).WithMany(p => p.Tranches)
                    .HasForeignKey(d => d.Numcite)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Tranche_Cite");
            });

            modelBuilder.Entity<Utilisateur>(entity =>
            {
                entity.HasKey(e => e.Codeuser).HasName("PK_Utilisateur");

                entity.ToTable("utilisateur");

                entity.Property(e => e.Codeuser)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("CODEUSER");
                entity.Property(e => e.Access)
                    .HasMaxLength(1)
                    .IsUnicode(false)
                    .HasColumnName("ACCESS");
                entity.Property(e => e.Actif).HasColumnName("ACTIF");
                entity.Property(e => e.Datefinu).HasColumnName("DATEFINU");
                entity.Property(e => e.Mdpuser)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("MDPUSER");
                entity.Property(e => e.Nomuser)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("NOMUSER");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    }
}
