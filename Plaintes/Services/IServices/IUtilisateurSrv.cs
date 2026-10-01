using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IUtilisateurSrv
    {
        Task UpdateNameUser(Utilisateur user, string newName);
    }
}
