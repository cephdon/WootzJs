﻿#region License

//-----------------------------------------------------------------------
// <copyright>
// The MIT License (MIT)
// 
// Copyright (c) 2014 Kirk S Woll
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

#endregion

using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.WootzJs;
using WootzJs.Mvc.Models;

namespace WootzJs.Mvc.Views.Binders
{
    public static class TextBoxBinders
    {
        public static void BindTextBox<TModel, TValue>(this Bindings<TModel> bindings, TextBox textBox, Expression<Func<TModel, TValue>> property) where TModel : Model<TModel>
        {
            var getter = property.Compile();
            var setter = property.GetPropertyInfo();
            var model = bindings.Model;

            Action updateText = () => textBox.Text = (string)Convert.ChangeType(getter(model), typeof(string));
            updateText();

            textBox.Changed += () => setter.SetValue(model, null);

            var prop = model.GetProperty(property);
            prop.Changed += updateText;
            prop.Validated += validations => 
            {
                var application = textBox.ViewContext.ControllerContext.Application;
                application.NotifyOnValidatedControl(textBox, validations);
            };
        }
    }
}