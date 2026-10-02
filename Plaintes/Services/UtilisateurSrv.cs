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
    }
}
