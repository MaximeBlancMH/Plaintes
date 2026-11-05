namespace Plaintes.Models
{
    public class PagedResult<T>
    {
        public List<T> Plaintes { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public int CoutMax { get; set; }
        // Bornes du curseur de période : plus ancienne et plus récente date de début des faits parmi les dossiers
        // qui répondent aux autres filtres (la période choisie n'est pas prise en compte)
        public DateOnly? PeriodeMin { get; set; }
        public DateOnly? PeriodeMax { get; set; }
    }
}
