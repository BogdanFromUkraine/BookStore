using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Utility;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class CategoryController : Controller
	{
		private readonly ICategoryRepository _categoryRepo;
		public CategoryController(ICategoryRepository db)
		{
			_categoryRepo = db;
		}
		public IActionResult Index()
		{
			var objCategoryList = _categoryRepo.GetAll().ToList();
			return View(objCategoryList);
		}

		public IActionResult Create()
		{
			return View();
		}

		[HttpPost]
		public IActionResult Create(Category obj)
		{
			if (obj.Name == obj.DisplayOrder.ToString())
			{
				ModelState.AddModelError("name", "The DisplayOrder cannot exactly match the Name.");
			}

			if (ModelState.IsValid)
			{
                _categoryRepo.Add(obj);
                _categoryRepo.Save();
				TempData["success"] = "Category created successfully";
				return RedirectToAction("Index");
			}
			return View();

		}

		// edit
		public IActionResult Edit(int? id)
		{
			if (id == null || id == 0)
			{
				return NotFound();
			}

			Category categoryFromDb = _categoryRepo.Get(u => u.Id == id);

			if (categoryFromDb == null)
			{
				return NotFound();
			}

			return View(categoryFromDb);
		}

		[HttpPost]
		public IActionResult Edit(Category obj)
		{
			//obj.Id = 0; // через те що є проблеми через sql сервер

			if (ModelState.IsValid)
			{
                _categoryRepo.Update(obj);
                _categoryRepo.Save();
				TempData["success"] = "Category updated successfully";
				return RedirectToAction("Index");
			}
			return View();

			}
		// delete
		public IActionResult Delete(int? id) // get метод
		{
			if (id == null || id == 0)
			{
				return NotFound();
			}

			Category categoryFromDb = _categoryRepo.Get(u => u.Id == id);

			if (categoryFromDb == null)
			{
				return NotFound();
			}

			return View(categoryFromDb);
		}

		[HttpPost, ActionName("Delete")]
		public IActionResult DeletePOST(int? id)  // post метод 
		{
			Category obj = _categoryRepo.Get(u => u.Id == id);
			if (obj == null)  return NotFound();
            _categoryRepo.Remove(obj);
            _categoryRepo.Save();
			TempData["success"] = "Category deleted successfully";
			return RedirectToAction("Index");
			
			

		}
	}
}
