using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IUtilisateurSrv
    {
        Task UpdateNameUser(int idUser, string newName);
    }
}
