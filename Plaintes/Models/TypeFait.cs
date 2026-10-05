namespace Plaintes.Models
{
    public partial class TypeFait
    {
        public string CodeFait { get; set; } = null!;

        public string LibFait { get; set; } = null!;

        public virtual ICollection<Plainte> Plaintes { get; set; } = new List<Plainte>();
    }
}
