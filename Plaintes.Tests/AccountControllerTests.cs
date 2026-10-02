using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Plaintes.Controllers;

namespace Plaintes.Tests
{
    public class AccountControllerTests
    {
        private static AccountController CreerController()
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new AccountController(new ConfigurationBuilder().Build())
            {
                ControllerContext = new ControllerContext(actionContext),
                Url = new UrlHelper(actionContext)
            };
        }

        [Fact]
        public void Login_UrlLocale_EstConserveeCommeRedirection()
        {
            var result = Assert.IsType<ChallengeResult>(CreerController().Login("/Dossiers?page=2"));

            Assert.Equal("/Dossiers?page=2", result.Properties!.RedirectUri);
        }

        [Theory]
        [InlineData("https://site-malveillant.fr")]
        [InlineData("//site-malveillant.fr")]
        [InlineData("http://localhost/autre")]
        public void Login_UrlExterne_EstRemplaceeParLaRacine(string returnUrl)
        {
            var result = Assert.IsType<ChallengeResult>(CreerController().Login(returnUrl));

            Assert.Equal("/", result.Properties!.RedirectUri);
        }

        [Fact]
        public void Deconnecte_RenvoieLaVue()
        {
            Assert.IsType<ViewResult>(CreerController().Deconnecte());
        }

        [Fact]
        public void AccessDenied_RedirigeVersLogin()
        {
            var result = Assert.IsType<RedirectToActionResult>(CreerController().AccessDenied());

            Assert.Equal("Login", result.ActionName);
        }
    }
}
