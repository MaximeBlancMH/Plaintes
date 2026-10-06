namespace Plaintes.Services.Export
{
    /// <summary>Réglages de la section "Export" d'appsettings (conversion Word -> PDF avec LibreOffice).</summary>
    public class ExportSettings
    {
        // Exécutable LibreOffice : "soffice" s'il est dans le PATH (Linux : /usr/bin/soffice), sinon le chemin complet
        // (Windows : C:\Program Files\LibreOffice\program\soffice.exe).
        public string SofficePath { get; set; } = "soffice";

        // Durée maximale d'une conversion ; au-delà, le processus LibreOffice est arrêté.
        public int TimeoutSeconds { get; set; } = 60;

        // Nombre de conversions LibreOffice menées en même temps (chacune a son profil, conservé entre deux conversions).
        // 2 convient à un serveur modeste ; chaque instance de LibreOffice utilise de la mémoire (≈ 150 Mo) et un cœur de processeur.
        public int ConversionsSimultanees { get; set; } = 2;

        // Dossier des fichiers temporaires de conversion ; vide = dossier temporaire du système.
        public string? DossierTemporaire { get; set; }
    }
}
