using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Models.ViewModels;
using WebApp.Utility;
using static System.Net.WebRequestMethods;

namespace WebAppBookStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {

        private readonly IShopingCartRepository _shopingCart;
        private readonly IApplicationUserRepository _userRepository;
        private readonly IOrderHeaderRepository _orderHeaderRepository;
        private readonly IOrderDetailRepository _orderDetailRepository;

		[BindProperty]
		public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IShopingCartRepository shopingCart, IApplicationUserRepository userRepository,
            IOrderHeaderRepository orderHeaderRepository, IOrderDetailRepository orderDetailRepository)
        {
            _shopingCart = shopingCart;
            _userRepository = userRepository;
            _orderHeaderRepository = orderHeaderRepository;
			_orderDetailRepository = orderDetailRepository;

		}

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _shopingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            foreach(var cart in ShoppingCartVM.ShoppingCartList) 
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count); 
            }
           
            return View(ShoppingCartVM);
        }

        public IActionResult Summary() 
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _shopingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _userRepository.Get(u => u.Id == userId);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
		public IActionResult SummaryPOST()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _shopingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product");

            ShoppingCartVM.OrderHeader.OrderDate = System.DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

			ApplicationUser applicationUser = _userRepository.Get(u => u.Id == userId);

			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPriceBasedOnQuantity(cart);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}

            if(applicationUser.CompanyId.GetValueOrDefault() == 0) 
            {
                //it is a regular customer account and we need to capture payment
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            } else 
            {
				//it is a company user
				ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
				ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
			}

            _orderHeaderRepository.Add(ShoppingCartVM.OrderHeader);
            _orderHeaderRepository.Save();
            foreach(var cart in ShoppingCartVM.ShoppingCartList) 
            {
                OrderDetail orderDetail = new() 
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count,
                };

                _orderDetailRepository.Add(orderDetail);
                _orderDetailRepository.Save();

			}		

            if(applicationUser.CompanyId.GetValueOrDefault() == 0) 
            {
                // it is a regular customer and we need to capture payment
                //stripe logic
                var domain = "https://localhost:7183/";
                var options = new SessionCreateOptions
                {
                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                    CancelUrl = domain + $"customer/cart/index",
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                };

                foreach(var item in ShoppingCartVM.ShoppingCartList) 
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Title,

                            }
                        },
                        Quantity = item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }


                var service = new SessionService();
                Session session = service.Create(options);
                _orderHeaderRepository.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _orderHeaderRepository.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);

            }

			return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
		}

        public IActionResult OrderConfirmation(int id) 
        {
            OrderHeader orderHeader = _orderHeaderRepository.Get(u => u.Id == id,
                includeProperties: "ApplicationUser");

            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                //this is an order by customer

                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _orderHeaderRepository.UpdateStripePaymentID(orderHeader.Id,
                        session.Id, session.PaymentIntentId);
                    _orderHeaderRepository.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _orderHeaderRepository.Save();
                }
                HttpContext.Session.Clear();
            }


            List<ShoppingCart> shoppingCarts = _shopingCart
                .GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            _shopingCart.RemoveRange(shoppingCarts);
            _shopingCart.Save();


            return View(id);
        }

		public IActionResult Plus(int cartId) 
        {
            var cartFromDb = _shopingCart.Get(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _shopingCart.Update(cartFromDb);
            _shopingCart.Save();
            return RedirectToAction("Index");
        }
        public IActionResult Minus(int cartId) 
        {
            var cartFromDb = _shopingCart.Get(u => u.Id == cartId);
            if (cartFromDb.Count <= 1)
            {
                //remove
                HttpContext.Session.SetInt32(SD.SessionCart, _shopingCart
               .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                _shopingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _shopingCart.Update(cartFromDb);
            }
            
            _shopingCart.Save();
            return RedirectToAction("Index");
        }
        public IActionResult Delete(int cartId) 
        {
            var cartFromDb = _shopingCart.Get(u => u.Id == cartId);
            HttpContext.Session.SetInt32(SD.SessionCart, _shopingCart
               .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
           _shopingCart.Remove(cartFromDb);
           _shopingCart.Save();
           return RedirectToAction("Index");
        }
        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart) 
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else 
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else 
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}
