using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.APP.Models;
using MTL.APP.Utils;

namespace MTL.APP.Services
{
    public class UserService
    {
        private readonly MTLContext _context;
        public UserService(MTLContext appDBContext)
        {
            _context = appDBContext;
        }

        public async Task<User> LoginAsync(AddKhachHangModel model)
        {
            string decrypt_pass = Configs.Encrypt(model.Password);
            var user = _context.Users.FirstOrDefault
                (
                    u => u.EmailAddress == model.EmailAddress
                    && u.Password == decrypt_pass
                );
            if (user == null)
            {
                // Invalid user name or password
                //Val = 1;
            }
            else if (user.Isactive != true)
            {
                // User inactive
                //Val = 2;
            }
            else
            {
                // Success
                //Val = 3;
            }
            return user;
        }

        public async Task<List<Company>> GetCompanyWithUserAsync(User _user)
        {
            if (_user.RoleId == 1) // Là Admin Code
            {
                var _UserCompany = from com in _context.Companies
                                   select com;
                return _UserCompany.ToList();
            }
            else
            {
                var _UserCompany = from usercom in _context.UserCompanies
                                   join com in _context.Companies on usercom.Companyid equals com.Id
                                   where (usercom.Userid == _user.UserId)
                                   //select r;                               
                                   select com;
                return _UserCompany.ToList();
            }
        }

        public async Task<User> GetUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user;
        }
        public async Task<List<MTL_GetMenuByRolesResult>> GetMenuByRolesResultAsync(int idcompany, int idrole, bool isAdminConfig)
        {
            return await _context.MTL_GetMenuByRoles(idcompany, idrole, isAdminConfig)
                .ToListAsync();
        }

        public async Task<List<MTL_GetAllUserCompanyResult>> GetAllUserCompanyAsync(int idcompany)
        {
            return await _context.MTL_GetAllUserCompany(idcompany)
                      .Where(b => b.IsActive == true)
                .ToListAsync();
        }

        public async Task<Company> GetCompanyAsync(int idcompany)
        {
            Company _com = await _context.Companies.FindAsync(idcompany);
            return _com;
        }
        public async Task<User> UpdateTaiKhoanAsync(User us, bool isUpdatePassword)
        {
            int result = 0;
            User _taikhoan = await _context.Users.FindAsync(us.UserId);
            if (_taikhoan != null)
            {
                if (isUpdatePassword)
                {
                    //update mật khẩu
                    string decrypt_pass = Configs.Encrypt(us.Password);
                    _taikhoan.Password = decrypt_pass;
                }
                else // update full
                {
                    //sửa ko update mật khẩu
                    _taikhoan.Fullname = us.Fullname;
                    _taikhoan.Phone = us.Phone;
                    _taikhoan.Diachi = us.Diachi;
                    _taikhoan.EmailAddress = us.EmailAddress;
                    _taikhoan.Isactive = us.Isactive;
                    _taikhoan.RoleId = us.RoleId;
                }
                result = await _context.SaveChangesAsync();
            }
            return _taikhoan;
        }
        public async Task<List<MTL_GetAllUserCompanyResult>> GetAllUserSettingAsync(int idcompany)
        {
            List<MTL_GetAllUserCompanyResult> list = await _context.MTL_GetAllUserCompany(idcompany)
                .Where(b => b.Roleid != 1)
                .ToListAsync();
            return list;
        }

        public async Task<User> ChangePassTaiKhoanAsync(User us)
        {
            int result = 0;
            User _taikhoan = await _context.Users.FindAsync(us.UserId);
            if (_taikhoan != null)
            {
                //sửa 
                string decrypt_pass = Configs.Encrypt(us.Password);
                _taikhoan.Password = decrypt_pass;
                result = await _context.SaveChangesAsync();
            }
            return _taikhoan;
        }

        public async Task<List<Role>> GetAllRoleAsync()
        {
            var exceptionList = new List<int> { 1 };
            return await _context.Roles
                     .Where(b => !exceptionList.Contains(b.RoleId))
                     .ToListAsync();
        }
        public async Task<List<MTL_GetMenuAllResult>> GetMenuAllResultAsync()
        {
            List<MTL_GetMenuAllResult> list = await _context.MTL_GetMenuAll()
                  .ToListAsync();
            return list;
        }

        public async Task<List<MTL_GetMenuByCompanyResult>> GetMenuByCompanyAsync(int idcompany)
        {
            List<MTL_GetMenuByCompanyResult> list = await _context.MTL_GetMenuByCompany(idcompany)
                  .ToListAsync();
            return list;
        }
        Company GetCompanyProperty(Company update_company, Company EditCompany, bool isNew)
        {
            update_company.Woourl = EditCompany.Woourl;
            update_company.Woosecret = EditCompany.Woosecret;
            update_company.Wookey = EditCompany.Wookey;
            update_company.Watermark = EditCompany.Watermark;
            update_company.Website = EditCompany.Website;
            update_company.Autocode = EditCompany.Autocode;
            update_company.Diachi = EditCompany.Diachi;
            update_company.Masothue = EditCompany.Masothue;
            update_company.Sodienthoai = EditCompany.Sodienthoai;
            update_company.Logourl = EditCompany.Logourl;
            update_company.Name = EditCompany.Name;
            update_company.Email = EditCompany.Email;
            update_company.Sotaikhoan = EditCompany.Sotaikhoan;
            update_company.Tennganhang = EditCompany.Tennganhang;
            update_company.Chutaikhoan = EditCompany.Chutaikhoan;
            update_company.Sodonghienthi = EditCompany.Sodonghienthi;
            update_company.IsActiveApi = EditCompany.IsActiveApi;
            update_company.IsActiveSpao = EditCompany.IsActiveSpao;
            update_company.Islock = EditCompany.Islock;
            update_company.IsBaoHanh = EditCompany.IsBaoHanh;
            update_company.IsThuongHieu = EditCompany.IsThuongHieu;
            update_company.IsKhoHang = EditCompany.IsKhoHang;

            update_company.UploadProductUrl = EditCompany.UploadProductUrl;
            update_company.UploadUrl = EditCompany.UploadUrl;

            if (!isNew) update_company.Id = EditCompany.Id;
            return update_company;
        }
        public async Task<Company> SaveCompanyAsync(Company EditCompany, bool isNew)
        {
            Company update_company = new Company();
            int result = 0;
            if (isNew)
            {
                //new 
                update_company = this.GetCompanyProperty(update_company, EditCompany, isNew);
                await _context.Companies.AddAsync(update_company);
            }
            else
            {
                //update
                update_company = _context.Companies.Single(a => a.Id == EditCompany.Id);
                update_company = this.GetCompanyProperty(update_company, EditCompany, isNew);
            }
            result = await _context.SaveChangesAsync();
            return update_company;
        }

        public async Task<Company> DeleteCompanyAsync(Company com)
        {
            Company _Company = await _context.Companies.FindAsync(com.Id);
            if (_Company != null)
            {
                _Company.Islock = true;
                //_context.Companies.Remove(_Company);
                await _context.SaveChangesAsync();
            }
            return _Company;
        }

        public async Task<ActionResult<User>> DeleteUserFromCompanyAsync(int iduser)
        {
            var user = await _context.Users.FindAsync(iduser);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            return user;
        }
        public async Task<User> DeleteUserFromCompanyAsync(User user, int idcompany)
        {
            int iduser = user.UserId;
            //Delete bảng User_Company
            var _usercom = await _context.UserCompanies.FindAsync(iduser, idcompany);
            if (_usercom != null)
            {
                _context.UserCompanies.Remove(_usercom);
                await _context.SaveChangesAsync();
            }
            //cap nhat IsDelete bảng User
            var _user = await _context.Users.FindAsync(iduser);
            if (user != null)
            {
                _user.Isdelete = true;
                await _context.SaveChangesAsync();
            }
            return user;
        }
        public async Task<RoleOfMenu> UpdateRoleWithMenuAsync(int idcompany, RoleOfMenu ROM)
        {
            int result = 0;
            await _context.RoleOfMenus.Where(
                        x => (x.Idcompany == idcompany)
                    && (x.Idrole == ROM.Idrole)
                    && (x.Idmenu == ROM.Idmenu)
            ).ExecuteDeleteAsync(); // xoá id trong bảng

            if (ROM.Enable == true)
            {
                // có active mới insert
                _context.RoleOfMenus.Add(ROM);
                result = await _context.SaveChangesAsync();
            }
            return ROM;
        }
        public async Task<MenuOfCompany> UpdateCompanyMenuAsync(int idcompany, int idmenu)
        {
            int result = 0;
            await _context.MenuOfCompanies.Where(
                        x => (x.Idcompany == idcompany)
                    && (x.Idmenu == idmenu)

            ).ExecuteDeleteAsync(); // xoá id trong bảng
            MenuOfCompany MOC = new MenuOfCompany();
            MOC.Enable = true;
            MOC.Idcompany = idcompany;
            MOC.Idmenu = idmenu;
            _context.MenuOfCompanies.Add(MOC);
            result = await _context.SaveChangesAsync();
            return MOC;
        }
        public async Task<MenuOfCompany> DeleteMenuCompanyAsync(MenuOfCompany MOC)
        {
            //Delete bảng MenuOfCompany
            MenuOfCompany _MenuOfCompany = await _context.MenuOfCompanies.FindAsync(MOC.Idcompany, MOC.Idmenu);
            if (_MenuOfCompany != null)
            {
                _context.MenuOfCompanies.Remove(_MenuOfCompany);
                await _context.SaveChangesAsync();
            }

            return _MenuOfCompany;
        }
        public async Task<User> InsertTaiKhoanAsync(User us, int idcompany)
        {
            int result = 0;

            _context.Users.Add(us);
            result = await _context.SaveChangesAsync();
            int iduser_new = us.UserId;

            UserCompany uscom = new UserCompany();
            uscom.Userid = iduser_new;
            uscom.IsActive = us.Isactive;
            uscom.Companyid = idcompany;

            _context.UserCompanies.Add(uscom);
            result = await _context.SaveChangesAsync();

            return us;
        }
        public async Task<List<Company>> GetAllCompanyAsync()
        {
            return await _context.Companies
                  //.Where(b => b.Idcompany == idcompany)
                  .ToListAsync();
        }
        public async Task<List<User>> GetAllUserAsync()
        {
            return await _context.Users.ToListAsync();
        }



    }
}
