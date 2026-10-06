namespace Plaintes.Services.Export
{
    public interface IConvertisseurPdf
    {
        /// <summary>Convertit un document Word (.doc ou .docx) en PDF. L'extension indique le format du contenu.</summary>
        Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default);
    }
}
