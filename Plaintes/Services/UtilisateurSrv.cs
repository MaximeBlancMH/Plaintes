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

        public async Task UpdateNameUser(Utilisateur user, string newName)
        {
            user.NomUser = newName;


            await _context.SaveChangesAsync();

        }
    }
}
