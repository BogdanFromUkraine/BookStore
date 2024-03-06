using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApp.DataAccess.Data;
using WebApp.DataAccess.Repository.IRepository;
using WebApp.Models;
using WebApp.Models.ViewModels;
using WebApp.Utility;

namespace WebAppBookStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class UserController: Controller
    {

        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        public UserController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IActionResult Index() 
        {
            
            return View();
        }

        public IActionResult RoleManagment(string userId) 
        {
            string RoleId = _db.UserRoles.FirstOrDefault(u => u.UserId == userId).RoleId;

            RoleManagmentVM RoleVM = new RoleManagmentVM() 
            {
                ApplicationUser = _db.ApplicationUsers.Include(u => u.Company)
                .FirstOrDefault(u => u.Id == userId),
                RoleList = _db.Roles.Select(i => new SelectListItem 
                {
                    Text = i.Name,
                    Value = i.Id
                }),
                CompanyList = _db.Companies.Select(i => new SelectListItem 
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
            };

            RoleVM.ApplicationUser.Role = _db.Roles.FirstOrDefault(u => u.Id == RoleId).Name;
            return View(RoleVM);
        }
        [HttpPost]
		public IActionResult RoleManagment(RoleManagmentVM roleManagmentVM)
		{
			string RoleId = _db.UserRoles.FirstOrDefault(u => u.UserId == roleManagmentVM.ApplicationUser.Id).RoleId;
            string oldRole = _db.Roles.FirstOrDefault(u => u.Id == RoleId).Name;

            if (!(roleManagmentVM.ApplicationUser.Role == oldRole)) 
            {
                //a role was updated
                ApplicationUser applicationUser = _db.ApplicationUsers
                    .FirstOrDefault(u => u.Id == roleManagmentVM.ApplicationUser.Id);
                if (roleManagmentVM.ApplicationUser.Role == SD.Role_Company) 
                {
                    applicationUser.CompanyId = roleManagmentVM.ApplicationUser.CompanyId;
                }
                if (oldRole == SD.Role_Company) 
                {
                    applicationUser.CompanyId = null;
                }
                _db.SaveChanges();

                _userManager.RemoveFromRoleAsync(applicationUser, oldRole).GetAwaiter().GetResult();
                _userManager.AddToRoleAsync(applicationUser, roleManagmentVM.ApplicationUser.Role).GetAwaiter().GetResult();
            }
		
			return RedirectToAction("Index");
		}

		#region API	CALLS
		[HttpGet]
        public IActionResult GetAll()
        {
            var objUserList = _db.ApplicationUsers.Include(u => u.Company).ToList();

            var userRoles = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();

            foreach (var user in objUserList) 
            {
                var roleId = userRoles.FirstOrDefault(u => u.UserId == user.Id).RoleId;
                user.Role = roles.FirstOrDefault(u => u.Id == roleId).Name;

                if(user.Company == null) 
                {
                    user.Company = new Company() { Name = "" };
                }
            }


            return Json(new { data = objUserList });
        }

        [HttpPost]
        public IActionResult LockUnlock([FromBody] string id)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if(objFromDb == null) 
            {
                return Json(new { succes = false, message = "Error while Locking/Unlocking" });
            }

            if(objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now) 
            {
                //user is currenty locked and we need to unlock them
                objFromDb.LockoutEnd = DateTime.Now;
            } else 
            {
                objFromDb.LockoutEnd = DateTime.Now.AddDays(300);
            }
            _db.SaveChanges();

            return Json(new { succes = true, message = "Delete Successful" });
        }


        #endregion

    }
}
