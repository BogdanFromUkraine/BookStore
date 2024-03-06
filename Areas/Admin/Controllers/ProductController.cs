using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebApp.Models.ViewModels;
using NuGet.Protocol.Plugins;
using Microsoft.AspNetCore.Authorization;
using WebApp.Utility;

namespace WebAppBookStore.Areas.Admin.Controllers
{
    [Area("Admin")]
	[Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo; // створив поле для category, щоб получити дані для SelectListItem 
		private readonly IWebHostEnvironment _webHostEnvironment;
		// можливо потрібно використати unitOfWork
        public ProductController(IProductRepository db, ICategoryRepository dbCategory, IWebHostEnvironment webHostEnvironment)
        {
            _productRepo = db;
			_categoryRepo = dbCategory; // за допомогою dependency injection получив дані у _categoryRepo
			_webHostEnvironment = webHostEnvironment;
		}
        public IActionResult Index()
        {
            var objProductList = _productRepo.GetAll(includeProperties:"Category").ToList();
            return View(objProductList);
        }

        public IActionResult Upsert(int? id)
        {
			// З ViewData все працює нормально, не має ніяких проблем і навіть працює валідація, а з ViewModel є проблеми
			//IEnumerable<SelectListItem> CategoryList = _categoryRepo.GetAll().Select(u => new SelectListItem
			//{
			//	Text = u.Name,
			//	Value = u.Id.ToString()
			//}); // отримав дані,
			//ViewData["CategoryList"] = CategoryList;
			//return View();

			ProductVM productVM = new()
			{
				CategoryList = _categoryRepo.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}), // отримав дані,
				Product = new Product()
			};

			if(id == null || id == 0) 
			{
				//create
				return View(productVM);

			} else 
			{
				//update
				productVM.Product = _productRepo.Get(u => u.Id == id);
				return View(productVM);
			}
			


		}

		[HttpPost]
        public IActionResult Upsert(ProductVM productVM, IFormFile? file)
        {
			if (ModelState.IsValid)
			{
				string wwwRootPath = _webHostEnvironment.WebRootPath;
				if (file != null)
				{
					string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
					string productPath = Path.Combine(wwwRootPath, @"images\product");

					if(!string.IsNullOrEmpty(productVM.Product.ImageUrl)) 
					{
						//delete the old image
						var oldImagePath = 
							Path.Combine(wwwRootPath, productVM.Product.ImageUrl.TrimStart('/')); // получаємо шлях старої фотки

						if(System.IO.File.Exists(oldImagePath)) // перевіряю чи існує файл по такому шляху
						{
							System.IO.File.Delete(oldImagePath); // видаляю стару фотку
						}
					}

					using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create)) 
					{
						file.CopyTo(fileStream);
					}

					productVM.Product.ImageUrl = @"\images\product\" + fileName;
				}

				if(productVM.Product.Id == 0) 
				{
					_productRepo.Add(productVM.Product);
				} else 
				{
					_productRepo.Update(productVM.Product);
				}
				_productRepo.Save();
				TempData["success"] = "product created successfully";
				return RedirectToAction("Index");
			}
			return View();
		}

        // edit
		// ЦЕЙ EDIT ТЕПЕР НЕ ПОТРІБЕН, ТОМУ ЩО ВІН РЕАЛІЗОВАНИЙ У UPSERT
  //      public IActionResult Edit(int? id)
  //      {
		//	if (id == null || id == 0)
		//	{
		//		return NotFound();
		//	}

		//	Product productFromDb = _productRepo.Get(u => u.Id == id);

		//	if (productFromDb == null)
		//	{
		//		return NotFound();
		//	}

		//	return View(productFromDb);
		//}

  //      [HttpPost]
  //      public IActionResult Edit(Product obj)
  //      {
		//	if (ModelState.IsValid)
		//	{
		//		_productRepo.Update(obj);
		//		_productRepo.Save();
		//		TempData["success"] = "product updated successfully";
		//		return RedirectToAction("Index");
		//	}
		//	return View();
		//}
        // delete
       
		#region API	CALLS
		[HttpGet]
		public IActionResult GetAll() 
		{
			var objProductList = _productRepo.GetAll(includeProperties: "Category").ToList();
			return Json(new { data = objProductList });
		}

		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			var productToBeDeleted = _productRepo.Get(u => u.Id == id);
			if(productToBeDeleted == null) 
			{
				return Json(new { succes = false, message = "Error while deleting" });
			}

			var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToBeDeleted.ImageUrl.TrimStart('/')); 

			if (System.IO.File.Exists(oldImagePath)) 
			{
				System.IO.File.Delete(oldImagePath); 
			}

			_productRepo.Remove(productToBeDeleted);
			_productRepo.Save();
			return Json(new { succes = true, message = "Delete Successful" });
		}
			
		
		#endregion

	}
}
