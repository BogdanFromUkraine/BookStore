using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Models.ViewModels;
using WebApp.Utility;

namespace WebAppBookStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Company)]
    public class CompanyController: Controller
    {
        private readonly ICompanyRepository _companyRepo;

        public CompanyController(ICompanyRepository companyRepo)
        {
            _companyRepo = companyRepo;
        }

        public IActionResult Index() 
        {
            var objCompanyList = _companyRepo.GetAll().ToList();
            return View(objCompanyList);
        }
        public IActionResult Upsert(int? id) 
        {
            if (id == null || id == 0)
            {
                //create
                return View(new Company());

            }
            else
            {
                //update
                Company companyObj = _companyRepo.Get(u => u.Id == id);
                return View(companyObj);
            }
        }
        [HttpPost]
        public IActionResult Upsert(Company companyObj) 
        {
            if(ModelState.IsValid) 
            {
                if (companyObj.Id == 0)
                {
                    _companyRepo.Add(companyObj);
                }
                else
                {
                    _companyRepo.Update(companyObj);
                }
                _companyRepo.Save();
                TempData["success"] = "product created successfully";
                return RedirectToAction("Index");
            } else 
            {
                return View(companyObj);
            }
            
        }
        public IActionResult Delete()
        {
            return View();
        }

        #region API	CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            var objCompanyList = _companyRepo.GetAll().ToList();
            return Json(new { data = objCompanyList });
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var companyToBeDeleted = _companyRepo.Get(u => u.Id == id);
            if (companyToBeDeleted == null)
            {
                return Json(new { succes = false, message = "Error while deleting" });
            }

          

            _companyRepo.Remove(companyToBeDeleted);
            _companyRepo.Save();
            return Json(new { succes = true, message = "Delete Successful" });
        }


        #endregion

    }
}
