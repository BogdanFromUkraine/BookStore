using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Utility;

namespace WebAppBookStore.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent 
    {
        private readonly IShopingCartRepository _shopingCartRepository;
        public ShoppingCartViewComponent(IShopingCartRepository shopingCartRepository)
        {
            _shopingCartRepository = shopingCartRepository;
        }
        public async Task<IViewComponentResult> InvokeAsync() 
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                if(HttpContext.Session.GetInt32(SD.SessionCart) == null) 
                {
                    HttpContext.Session.SetInt32(SD.SessionCart,
                   _shopingCartRepository.GetAll(u => u.ApplicationUserId == claim.Value).Count());
                }
               
                return View(HttpContext.Session.GetInt32(SD.SessionCart));
            } else 
            {
                HttpContext.Session.Clear();
                return View(0);
            }
        }
    }
}
