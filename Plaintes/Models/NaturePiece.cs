namespace Plaintes.Models
{
    public partial class NaturePiece
    {
        public string CodeNature { get; set; } = null!;

        public string LibNature { get; set; } = null!;

        public virtual ICollection<PieceJointe> PieceJointes { get; set; } = new List<PieceJointe>();
    }
}
