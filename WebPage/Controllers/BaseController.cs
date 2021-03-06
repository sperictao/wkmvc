﻿using Common;
using Service;
using Service.IService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebPage.Controllers
{
    public class BaseController : Controller
    {
        #region 公用变量
        /// <summary>
        /// 查询关键词
        /// </summary>
        public string keywords { get; set; }
        /// <summary>
        /// 视图传递的分页页码
        /// </summary>
        public int page { get; set; }
        /// <summary>
        /// 视图传递的分页条数
        /// </summary>
        public int pagesize { get; set; }
        /// <summary>
        /// 用户容器，公用
        /// </summary>
        public IUserManage UserManage = Spring.Context.Support.ContextRegistry.GetContext().GetObject("Service.User") as IUserManage;
        
        #endregion

        #region 用户对象
        /// <summary>
        /// 获取当前用户对象
        /// </summary>
        public Account CurrentUser
        {
            get
            {
                //从Session中获取用户对象
                if (SessionHelper.GetSession("CurrentUser") != null)
                {
                    return SessionHelper.GetSession("CurrentUser") as Account;
                }
                //Session过期 通过Cookies中的信息 重新获取用户对象 并存储于Session中
                var account = UserManage.GetAccountByCookie();
                SessionHelper.SetSession("CurrentUser", account);
                return account;
            }
        }
        #endregion

        #region 重写控制器 OnActionExecuting(ActionExecutingContext filterContext)方法 实现登录验证和公共变量的获取

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            #region 登录用户验证
            //1.判断Session对象是否存在和登录验证 
            if (filterContext.HttpContext.Session == null || this.CurrentUser == null)
            {
                filterContext.HttpContext.Response.Write(
                    "<script type='text/javascript'> alert('登录已过期，请重新登录');window.top.location='/';</script>"
                    );
                filterContext.RequestContext.HttpContext.Response.End();
                filterContext.Result = new EmptyResult();
                return;
            }

            #endregion
            #region 公共Get变量
            //分页页码
            object p = filterContext.HttpContext.Request["page"];
            if (p == null || p.ToString() == "")
            {
                page = 1;
            }
            else
            {
                page = int.Parse(p.ToString());
            }

            //搜索关键词
            string search = filterContext.HttpContext.Request.QueryString["Search"];
            if (!string.IsNullOrEmpty(search))
            {
                keywords = search;
            }

            //显示分页条数
            string size = filterContext.HttpContext.Request.QueryString["example_length"];
            if (!string.IsNullOrEmpty(size) && System.Text.RegularExpressions.Regex.IsMatch(size.ToString(), @"^\d+$"))
            {
                pagesize = int.Parse(size.ToString());
            }
            else
            {
                pagesize = 10;
            }
            #endregion

            //base.OnActionExecuting(filterContext);
        }
        #endregion

    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class UserAuthorizeAttribute : AuthorizeAttribute
    {
        #region 字段和属性
        /// <summary>
        /// 模块别名，可配置更改
        /// </summary>
        public string ModuleAlias { get; set; }
        /// <summary>
        /// 权限动作
        /// </summary>
        public string OperaAction { get; set; }
        /// <summary>
        /// 权限访问控制器参数
        /// </summary>
        private string Sign { get; set; }
        /// <summary>
        /// 基类实例化
        /// </summary>
        public BaseController baseController = new BaseController();

        #endregion

        /// <summary>
        /// 权限认证
        /// </summary>
        /// <param name="filterContext"></param>
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            //1、判断模块是否对应
            if (string.IsNullOrEmpty(ModuleAlias))
            {
                filterContext.HttpContext.Response.Write(" <script type='text/javascript'> alert('^您没有访问该页面的权限！'); </script>");
                filterContext.RequestContext.HttpContext.Response.End();
                filterContext.Result = new EmptyResult();
                return;
            }
            //2.判断用户是否存在
            if(baseController.CurrentUser == null)
            {
                filterContext.HttpContext.Response.Write(" <script type='text/javascript'> alert('^登录已过期，请重新登录！');window.top.location='/'; </script>");
                filterContext.RequestContext.HttpContext.Response.End();
                filterContext.Result = new EmptyResult();
                return;
            }

            //对比变量，用于权限认证
            var alias = ModuleAlias;

            #region 配置Sign调取控制器标识
            Sign = filterContext.RequestContext.HttpContext.Request.QueryString["sign"];
            if (!string.IsNullOrEmpty(Sign))
            {
                if (("," + ModuleAlias.ToLower()).Contains("," + Sign.ToLower()))
                {
                    alias = Sign;
                    filterContext.Controller.ViewData["Sign"] = Sign;
                }
            }
            #endregion

            //3.调用下面的方法，验证是否有访问此页面的权限，查看加操作
            var moduleId = baseController.CurrentUser.Modules.Where(p => p.ALIAS.ToLower() == alias.ToLower()).Select(p => p.ID).FirstOrDefault();
            bool _blAllowed = this.IsAllowed(baseController.CurrentUser, moduleId, OperaAction);
            if (!_blAllowed)
            {
                filterContext.HttpContext.Response.Write("<script type='text/javascript'> alert('您没有访问当前页面的权限！');</script>");
                filterContext.RequestContext.HttpContext.Response.End();
                filterContext.Result = new EmptyResult();
                return;
            }

            //4.有权限访问页面，将此页面的权限集合传给页面
            filterContext.Controller.ViewData["PermissionList"] = GetPermissionByJson(baseController.CurrentUser, moduleId);
            //base.OnAuthorization(filterContext);
        }
        /// <summary>
        /// 获取操作权限Json字符串，供视图JS判断使用
        /// </summary>
        /// <param name="account"></param>
        /// <param name="moduleId"></param>
        /// <returns></returns>
        string GetPermissionByJson(Account account, int moduleId)
        {
            //操作权限
            var _varPerListThisModule = account.Permissions.Where(p => p.MODULEID == moduleId).Select(R => new { R.PERVALUE }).ToList();
            return Common.JsonConverter.Serialize(_varPerListThisModule);
        }
        /// <summary>
        /// 功能描述：判断用户是否有此模块的操作权限
        /// </summary>
        /// <param name="user"></param>
        /// <param name="moduleId"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        bool IsAllowed(Account user, int moduleId, string action)
        {
            //判断入口
            if(user == null || user.Id <= 0 || moduleId == 0 || string.IsNullOrEmpty(action))
            {
                return false;
            }
            //验证权限
            var permission = user.Permissions.Where(p => p.MODULEID == moduleId);
            action = action.Trim(',');
            if (action.IndexOf(',') > 0)
            {
                permission = permission.Where(p => action.ToLower().Contains(p.PERVALUE.ToLower()));
            }
            else
            {
                permission = permission.Where(p => p.PERVALUE.ToLower() == action.ToLower());
            }
            return permission.Any();
        }
    }
    /// <summary>
    /// 模型去重，非常重要
    /// </summary>
    public class ModuleDistinct : IEqualityComparer<Domain.SYS_MODULE>
    {
        public bool Equals(Domain.SYS_MODULE x, Domain.SYS_MODULE y)
        {
            return x.ID == y.ID;
        }

        public int GetHashCode(Domain.SYS_MODULE obj)
        {
            return obj.ToString().GetHashCode();
        }
    }

}