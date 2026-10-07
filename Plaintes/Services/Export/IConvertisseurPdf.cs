namespace Plaintes.Services.Export
{
    public interface IConvertisseurPdf
    {
        Task<byte[]> ConvertirEnPdfAsync(byte[] contenu, string extension, CancellationToken ct = default);
    }
}
