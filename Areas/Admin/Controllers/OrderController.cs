using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Stripe.Climate;
using System.Diagnostics;
using System.Security.Claims;
using WebApp.DataAccess.Repository;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Models.ViewModels;
using WebApp.Utility;

namespace WebAppBookStore.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize]
	public class OrderController : Controller
    {
        private readonly IOrderHeaderRepository _orderHeaderRepo;
		private readonly IOrderDetailRepository _orderDetailRepo;
		[BindProperty]
		public OrderVM OrderVM { get; set; }

        public OrderController(IOrderHeaderRepository orderHeaderRepo, IOrderDetailRepository orderDetailRepository)
        {
            _orderHeaderRepo = orderHeaderRepo;
			_orderDetailRepo = orderDetailRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

		public IActionResult Details(int orderId) 
		{
            OrderVM = new()
			{
				OrderHeader = _orderHeaderRepo.Get(u => u.Id == orderId, includeProperties: "ApplicationUser"),
				OrderDetail = _orderDetailRepo.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product"),
			};
			return View(OrderVM);
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult UpdateOrderDetail()
        {
			var orderHeaderFromDb = _orderHeaderRepo.Get(u => u.Id == OrderVM.OrderHeader.Id);

			orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
			orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
			orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
			orderHeaderFromDb.City = OrderVM.OrderHeader.City;
			orderHeaderFromDb.State = OrderVM.OrderHeader.State;
			orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;

			if(!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier)) 
			{
				orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
			}
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
            {
                orderHeaderFromDb.Carrier = OrderVM.OrderHeader.TrackingNumber;
            }

			_orderHeaderRepo.Update(orderHeaderFromDb);
			_orderHeaderRepo.Save();

            return RedirectToAction(nameof(Details), new {orderId = orderHeaderFromDb.Id});
        }

		[HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

		public IActionResult StartProcessing() 
		{
			_orderHeaderRepo.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
			_orderHeaderRepo.Save();

			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

        public IActionResult ShipOrder()
        {
			var orderHeader = _orderHeaderRepo.Get(u => u.Id == OrderVM.OrderHeader.Id);
			//обновляю carrier i trackingNumber та обновляю OrderStatus
			orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
			orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
			orderHeader.OrderStatus = SD.StatusShipped;
			orderHeader.ShippingDate = DateTime.Now;

			if(orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				orderHeader.PaymentDueDate = DateTime.Now.AddDays(30);
			}

			_orderHeaderRepo.Update(orderHeader);
            _orderHeaderRepo.Save();

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

		public IActionResult CancelOrder() 
		{
			var orderHeader = _orderHeaderRepo.Get(u => u.Id == OrderVM.OrderHeader.Id);

			if(orderHeader.PaymentStatus == SD.PaymentStatusApproved) 
			{
				var options = new RefundCreateOptions
				{
					Reason = RefundReasons.RequestedByCustomer,
					PaymentIntent = orderHeader.PaymentIntentId
				};
				var service = new RefundService();
				Refund refund = service.Create(options);

				_orderHeaderRepo.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);

			} else 
			{
                _orderHeaderRepo.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
            }

			_orderHeaderRepo.Save();
			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}

		[ActionName("Details")]
        [HttpPost]
		public IActionResult Details_PAY_NOW() 
		{

			OrderVM.OrderHeader = _orderHeaderRepo
				.Get(u => u.Id == OrderVM.OrderHeader.Id, includeProperties: "ApplicationUser");
			OrderVM.OrderDetail = _orderDetailRepo
				.GetAll(u => u.OrderHeaderId == OrderVM.OrderHeader.Id, includeProperties: "Product");

            var domain = "https://localhost:7183/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVM.OrderHeader.Id}",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
            };

            foreach (var item in OrderVM.OrderDetail)
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
            _orderHeaderRepo.UpdateStripePaymentID(OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
            _orderHeaderRepo.Save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        public IActionResult PaymentConfirmation(int orderHeaderId)
        {
            OrderHeader orderHeader = _orderHeaderRepo.Get(u => u.Id == orderHeaderId);

            if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                //this is an order by company

                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _orderHeaderRepo.UpdateStripePaymentID(orderHeaderId,
                        session.Id, session.PaymentIntentId);
                    _orderHeaderRepo.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                    _orderHeaderRepo.Save();
                }
            }

            return View(orderHeaderId);
        }

        #region API	CALLS
        [HttpGet]
		public IActionResult GetAll(string status)
		{
			IEnumerable<OrderHeader> objOrderHeaders;

			if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				objOrderHeaders = _orderHeaderRepo.GetAll(includeProperties: "ApplicationUser").ToList();


			} else 
			{
				var claimsIdentity = (ClaimsIdentity)User.Identity;
				var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

				objOrderHeaders = _orderHeaderRepo
					.GetAll(u => u.ApplicationUserId == userId, includeProperties:"ApplicationUser");
			}


			switch (status)
			{
				case "pending":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
					break;
				case "inprocess":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.StatusInProcess);
					break;
				case "completed":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.StatusShipped);
					break;
				case "approved":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.StatusApproved);
					break;
				default:
					break;
			}



			return Json(new { data = objOrderHeaders });
        }

        #endregion
    }

}
