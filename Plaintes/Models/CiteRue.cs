namespace Plaintes.Models
{
    public class CiteRue
    {
        public int NumCite { get; set; }

        public int CodeRue { get; set; }

        public virtual Cite NumCiteNavigation { get; set; } = null!;

        public virtual Rue CodeRueNavigation { get; set; } = null!;
    }
}
