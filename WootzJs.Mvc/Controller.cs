﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WootzJs.Models;
using WootzJs.Mvc.Routes;
using WootzJs.Mvc.Views;

namespace WootzJs.Mvc
{
    public class Controller
    {
        public ControllerContext ControllerContext { get; private set; }
        public NavigationContext NavigationContext { get; private set; }
        public IActionInvoker ActionInvoker { get; private set; }

        protected virtual void Initialize(MvcApplication application, NavigationContext requestContext)
        {
            NavigationContext = requestContext;
            ActionInvoker = new ControllerActionInvoker();
            ControllerContext = new ControllerContext { Application = application, NavigationContext = NavigationContext, Controller = this };
        }

        /// <summary>
        /// The job of this method is invoke an action and ultimately populate the 
        /// </summary>
        /// <param name="application">The current application</param>
        /// <param name="context">A context which stores the request and response contexts.</param>
        public async Task Execute(MvcApplication application, NavigationContext context)
        {
            Initialize(application, context);
            var action = RouteData.Action;
            var actionResult = await ActionInvoker.InvokeAction(ControllerContext, action);
            var view = ((ViewResult)actionResult).View;
            view.Initialize(application.CreateViewContext(this));
            context.Response.View = view;
        }

        public RouteData RouteData
        {
            get { return NavigationContext.Request.RouteData; }
        }

        public MvcApplication Application
        {
            get { return ControllerContext.Application; }
        }

        protected ViewResult View()
        {
            var viewType = FindView();
            var view = (View)ControllerContext.Application.DependencyResolver.GetService(viewType);
            return new ViewResult(view);
        }

        protected ViewResult View(Model model)
        {
            ControllerContext.Application.NotifyBindModel(this, model);

            var viewType = FindView();
            var view = (View)ControllerContext.Application.DependencyResolver.GetService(viewType, 
                new Dictionary<Type, object> { { typeof(Model), model } });
            return new ViewResult(view);
        }

        protected ViewResult View(View view)
        {
            return new ViewResult(view);
        }

        protected virtual Type FindView()
        {
            var fullName = GetType().FullName;
            var controllerNamespace = fullName.Substring(0, fullName.LastIndexOf('.'));
            var rootNamespace = controllerNamespace.ChopEnd(".Controllers");
            var viewNamespace = rootNamespace + ".Views";
            var controllerName = GetType().Name;
            controllerName = controllerName.ChopEnd("Controller");
            var controllerViewNamespace = viewNamespace + "." + controllerName;
            var action = ControllerContext.NavigationContext.Request.RouteData.Action.Name;
            var viewName = action;
            viewName = controllerViewNamespace + "." + viewName + "View";

            var type = GetType().Assembly.GetType(viewName);
            if (type == null)
                throw new Exception(string.Format("No view found for {0}.{1} at {2}", GetType().FullName, action, viewName));
            return type;
        }
    }
}