using Plaintes.Models;

namespace Plaintes.Services.IServices
{
    public interface IUtilisateurSrv
    {
        Task UpdateNameUser(int idUser, string newName);
        Task UpdateUser(int idUser, string? newName, string newEmail, string newRole);
        Task<List<Utilisateur>> GetAllUsers();
        Task<Utilisateur?> GetUserById(int idUser);
        Task DeleteUser(int idUser);
        Task AddUser(string email, string role);
    }
}
