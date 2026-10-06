using Microsoft.EntityFrameworkCore;
using Plaintes.Data;
using Plaintes.Models;
using Plaintes.Services.IServices;

namespace Plaintes.Services
{
    public class UtilisateurSrv : IUtilisateurSrv
    {

        private readonly AppDbContext _context;
        private readonly ILogger<UtilisateurSrv> _logger;

        public UtilisateurSrv(AppDbContext context, ILogger<UtilisateurSrv> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateNameUser(int idUser, string newName)
        {
            var user = await _context.Utilisateurs.FindAsync(idUser);
            if (user == null)
            {
                _logger.LogWarning($"Utilisateur with ID {idUser} not found.");
                return;
            }
            user.NomUser = newName;


            await _context.SaveChangesAsync();

        }

        public async Task UpdateUser(int idUser, string newName, string newEmail, string newRole)
        {
            var user = await _context.Utilisateurs.FindAsync(idUser);
            if (user == null)
            {
                _logger.LogWarning($"Utilisateur with ID {idUser} not found.");
                return;
            }
            user.NomUser = newName;
            user.UserEmail = newEmail;
            user.Access = newRole;
            await _context.SaveChangesAsync();
        }

        public async Task<List<Utilisateur>> GetAllUsers()
        {
            return await _context.Utilisateurs.ToListAsync();
        }

        public async Task<Utilisateur?> GetUserById(int idUser)
        {
            return await _context.Utilisateurs.FindAsync(idUser);
        }

        public async Task DeleteUser(int idUser)
        {
            var user = await _context.Utilisateurs.FindAsync(idUser);
            if (user == null)
            {
                _logger.LogWarning($"Utilisateur with ID {idUser} not found.");
                return;
            }

            user.DateFinu = DateOnly.FromDateTime(DateTime.Now);
            await _context.SaveChangesAsync();
        }

        public async Task AddUser(string name, string email, string role)
        {
            var user = new Utilisateur
            {
                NomUser = name,
                UserEmail = email,
                Access = role,
            };

            _context.Utilisateurs.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}
