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

        /// <summary>
        /// Fonction qui modifie seulement le nom de l'utilisateur renseigné. On s'en sert lors de la 
        /// première connexion via Microsoft SAML d'un nouvel utilisateur.
        /// </summary>
        /// <param name="idUser">Id de l'utilisateur</param>
        /// <param name="newName">Nouveau nom</param>
        /// <returns></returns>
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

        /// <summary>
        /// Fonction qui modifie l'utilisateur
        /// </summary>
        /// <param name="idUser">Id de l'utilisateur</param>
        /// <param name="newName">Nouveau nom de l'utilisateur (null si l'utilisateur n'en a pas encore : il le reçoit de Microsoft à sa première connexion)</param>
        /// <param name="newEmail">Nouvel email de l'utilisateur</param>
        /// <param name="newRole">Nouveaux droits de l'utilisateur</param>
        /// <returns></returns>
        public async Task UpdateUser(int idUser, string? newName, string newEmail, string newRole)
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

        /// <summary>
        /// Fonction qui récupère tous les utilisateurs de la base de données
        /// </summary>
        /// <returns>Une liste contenant tous les utilisateurs</returns>
        public async Task<List<Utilisateur>> GetAllUsers()
        {
            return await _context.Utilisateurs.ToListAsync();
        }

        /// <summary>
        /// Fonction qui récupère un utilisateur grâce à son Id
        /// </summary>
        /// <param name="idUser">Id de l'utilisateur</param>
        /// <returns>Un utilisateur ou null s'il ne trouve rien</returns>
        public async Task<Utilisateur?> GetUserById(int idUser)
        {
            return await _context.Utilisateurs.FindAsync(idUser);
        }

        /// <summary>
        /// Fonction qui "supprime" un utilisateur. On passe sa DateFinu à aujourd'hui pour notifier que sa période d'utilisation est terminée
        /// </summary>
        /// <param name="idUser">Id de l'utilisateur</param>
        /// <returns></returns>
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

        /// <summary>
        /// Fonction qui permet d'ajouter un utilisateur dans la base de données.
        /// </summary>
        /// <param name="email">Email du nouvel utilisateur</param>
        /// <param name="role">Droits du nouvel utilisateur</param>
        /// <returns></returns>
        public async Task AddUser(string email, string role)
        {
            var user = new Utilisateur
            {
                UserEmail = email,
                Access = role,
            };

            _context.Utilisateurs.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}
