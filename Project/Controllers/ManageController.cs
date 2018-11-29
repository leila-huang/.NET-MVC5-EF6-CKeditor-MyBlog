﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Project.App_Start;
using Project.DAL;
using Project.Models;

namespace Project.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private BlogContext db = new BlogContext();
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ManageController()
        {
        }

        public ManageController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Manage/Index
        public ActionResult Index(ManageMessageId? message)
        {
            var name = User.Identity.GetUserName();
            //升序显示数据
            var article = db.Article.Where(w=>w.UserId== name).OrderByDescending(q => q.ArticleTime).Take(10).ToList();
            var back = db.Back.Where(w => w.UserId == name).OrderByDescending(q => q.BackTime).Take(10).ToList();
            var model = new ManageListModel();
           
            model.ArtcleList = article;
            model.BackImage = back;
            return View(model);
        }
       
        //
        // POST: /Manage/RemoveLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            ManageMessageId? message;
            var result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("ManageLogins", new { Message = message });
        }

        //
        // GET: /Manage/AddPhoneNumber
        public ActionResult AddPhoneNumber()
        {
            return View();
        }

        //
        // POST: /Manage/AddPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // 生成令牌并发送该令牌
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), model.Number);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "你的安全代码是: " + code
                };
                await UserManager.SmsService.SendAsync(message);
            }
            return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });
        }

        //
        // POST: /Manage/EnableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EnableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId(), true);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // POST: /Manage/DisableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DisableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId(), false);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // GET: /Manage/VerifyPhoneNumber
        public async Task<ActionResult> VerifyPhoneNumber(string phoneNumber)
        {
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), phoneNumber);
            // 通过 SMS 提供程序发送短信以验证电话号码
            return phoneNumber == null ? View("Error") : View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber });
        }

        //
        // POST: /Manage/VerifyPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePhoneNumberAsync(User.Identity.GetUserId(), model.PhoneNumber, model.Code);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.AddPhoneSuccess });
            }
            // 如果我们进行到这一步时某个地方出错，则重新显示表单
            ModelState.AddModelError("", "无法验证电话号码");
            return View(model);
        }

        //
        // POST: /Manage/RemovePhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemovePhoneNumber()
        {
            var result = await UserManager.SetPhoneNumberAsync(User.Identity.GetUserId(), null);
            if (!result.Succeeded)
            {
                return RedirectToAction("Index", new { Message = ManageMessageId.Error });
            }
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", new { Message = ManageMessageId.RemovePhoneSuccess });
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);
            return View(model);
        }

        //
        // GET: /Manage/SetPassword
        public ActionResult SetPassword()
        {
            return View();
        }

        //
        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }
                    return RedirectToAction("Index", new { Message = ManageMessageId.SetPasswordSuccess });
                }
                AddErrors(result);
            }

            // 如果我们进行到这一步时某个地方出错，则重新显示表单
            return View(model);
        }

        //
        // GET: /Manage/ManageLogins
        public async Task<ActionResult> ManageLogins(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.RemoveLoginSuccess ? "已删除外部登录名。"
                : message == ManageMessageId.Error ? "出现错误。"
                : "";
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(User.Identity.GetUserId());
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewBag.ShowRemoveButton = user.PasswordHash != null || userLogins.Count > 1;
            return View(new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins
            });
        }

        //
        // POST: /Manage/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // 请求重定向至外部登录提供程序，以链接当前用户的登录名
            return new AccountController.ChallengeResult(provider, Url.Action("LinkLoginCallback", "Manage"), User.Identity.GetUserId());
        }

        //
        // GET: /Manage/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
            return result.Succeeded ? RedirectToAction("ManageLogins") : RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        //
        // Get: /Manage/EditArticle
        public ActionResult EditArticle()
        {
            var articleList = db.ArticleSort.OrderBy(q => q.SortArticleName).ToList();
            ViewData["HourList"] = new SelectList(articleList, "SortArticleId", "SortArticleName");

            ViewBag.Title = "编辑文章";
            return View();
        }


        //
        // Post: /Manage/EditArticle
        [HttpPost]
        [AllowAnonymous]
        [ValidateInput(false)]
        public ActionResult EditArticle(Article article)
        {
            var imgstr = Request.Form["imgstr"] as string;
            var index = imgstr.IndexOf(";base64,") + 8;
            imgstr = imgstr.Substring(index, imgstr.Length - index);

            var img = ImgHelper.Base64StringToImage(imgstr);
            var rootPath = AppDomain.CurrentDomain.BaseDirectory;

            var imgPath = "/Upload/Image/" + Guid.NewGuid() + ".jpg";

            //var articleList = db.ArticleSort.OrderBy(q => q.SortArticleName).ToList();
            //ViewData["HourList"] = new SelectList(articleList, "SortArticleId", "SortArticleName");
            //一些文章内部属性
            var sort = db.ArticleSort.Where(w => w.SortArticleId == article.SortArticleId).FirstOrDefault();
            article.SortArticleName = sort.SortArticleName;
            article.ArticleTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            article.UserId = User.Identity.GetUserName();
            article.ArticleCover = imgPath;
            article.ArticleClick = 0;
            db.Article.Add(article);

            if (db.SaveChanges() > 0)
            {
                if (img != null)
                {
                    img.Save(rootPath + imgPath);
                }
                return RedirectToAction("Index", "Manage");
            }

            return View();
        }
        //
        // Get: /Manage/EditBackImage
        public ActionResult EditBackImage()
        {
            ViewBag.Title = "上传壁纸&句子";
            return View();
        }
        //
        // Post: /Manage/EditBackImage
        [HttpPost]
        public ActionResult EditBackImage(BackImage b)
        {   //上传图片处理
            var imgstr = Request.Form["imgstr"] as string;
            var index = imgstr.IndexOf(";base64,") + 8;
            imgstr = imgstr.Substring(index, imgstr.Length - index);

            var img = ImgHelper.Base64StringToImage(imgstr);
            var rootPath = AppDomain.CurrentDomain.BaseDirectory;

            var imgPath = "/Upload/BackImage/" + Guid.NewGuid() + ".jpg";

            b.BackTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            b.UserId = User.Identity.GetUserName();
            b.BackImg = imgPath;

            db.Back.Add(b);
            if (db.SaveChanges() > 0)
            {
                if (img != null)
                {
                    img.Save(rootPath + imgPath);
                }
                return RedirectToAction("Index", "Manage");
            }
            return View();
        }
        //
        // Post: /Manage/DeteleBackImage
        [HttpPost]
        public ActionResult DeteleBackImage(int id)
        {
            var imgInfo = db.Back.Where(w => w.BackImgId == id).FirstOrDefault();
            if (null != imgInfo)
            {
                db.Back.Remove(imgInfo);
                db.SaveChanges();
            }
            return RedirectToAction("Index", "Manage");
        }
        //
        // Post: /Manage/DeteleBackImage
        [HttpPost]
        public ActionResult DeteleArticle(int id)
        {
            var imgInfo = db.Article.Where(w => w.ArticleId == id).FirstOrDefault();
            if (null != imgInfo)
            {
                db.Article.Remove(imgInfo);
                db.SaveChanges();
            }
        
            return RedirectToAction("Index", "Manage");
        }
   
        [HttpPost]
        public JsonResult Upload(HttpPostedFileBase upload)
        {
            string savePath = "/Upload/Image/";
            string dirPath = Server.MapPath(savePath);
            //如果目录不存在则创建目录 
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            //获取图片文件名及扩展名
            var fileName = Path.GetFileName(upload.FileName);
            string fileExt = Path.GetExtension(fileName).ToLower();
            //用时间来生成新文件名并保存
            string newFileName = DateTime.Now.ToString("yyyyMMddHHmmss_ffff", DateTimeFormatInfo.InvariantInfo) + fileExt; upload.SaveAs(dirPath + "/" + newFileName);
            //上传成功后，我们还需要返回Json格式的响应
            return Json(
                new { uploaded = 1, fileName = newFileName, url = savePath + newFileName });
        }

        #region 帮助程序
        // 用于在添加外部登录名时提供 XSRF 保护
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }

        #endregion
    }
}