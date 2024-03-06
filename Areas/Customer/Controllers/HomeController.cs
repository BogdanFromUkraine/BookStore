using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Diagnostics;
using System.Security.Claims;
using WebApp.DataAccess.Repository;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Utility;

namespace WebAppBookStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _productRepo;
        private readonly IShopingCartRepository _shopingCartRepository;

        public HomeController(ILogger<HomeController> logger, IProductRepository productRepo, IShopingCartRepository shopingCartRepository)
        {
            _logger = logger;
            _productRepo = productRepo;
            _shopingCartRepository = shopingCartRepository;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _productRepo.GetAll(includeProperties: "Category");
            return View(productList);
        }   
        public IActionResult Details(int productId)
        {
            ShoppingCart cart = new()
            {
                Product = _productRepo.Get(u => u.Id == productId, includeProperties: "Category"),
                Count = 1,
                ProductId = productId
            };
            
            return View(cart);
        }
        [HttpPost]
        [Authorize]
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
         
            shoppingCart.ApplicationUserId = userId;

            ShoppingCart cartFromDb = _shopingCartRepository.Get(u => u.ApplicationUserId == userId && 
            u.ProductId == shoppingCart.ProductId);

            if (cartFromDb != null) 
            {
                //shopping cart exists
                cartFromDb.Count += shoppingCart.Count;
                _shopingCartRepository.Update(cartFromDb);
                _shopingCartRepository.Save();


            } else 
            {
                //add cart record
                _shopingCartRepository.Add(shoppingCart);
                _shopingCartRepository.Save();
                HttpContext.Session.SetInt32(SD.SessionCart,
                    _shopingCartRepository.Get(u => u.ApplicationUserId == userId &&
            u.ProductId == shoppingCart.ProductId).Count);
                
            }

            TempData["success"] = "Cart updated successfully";
           
            ;

            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}