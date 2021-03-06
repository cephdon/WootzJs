#region License
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

using System.Runtime.InteropServices;
using System.Runtime.WootzJs;

namespace System
{
	[StructLayout(LayoutKind.Auto)]
	public struct Char
	{
		public static explicit operator string(char ch)
		{
			return null;
		}

        public static bool IsWhiteSpace(char c)
        {
            return Jsni.regex("\\s").test(c.As<JsString>());
        }

        public static bool IsDigit(char c)
        {
            return Jsni.regex("^\\d+$").test(c.As<JsString>());
        }

        public static char ToUpper(char c)
        {
            return c.ToString().ToUpper()[0];
        }

        public static char ToLower(char c)
        {
            return c.ToString().ToLower()[0];
        }

        public static bool IsUpper(char c)
        {
            return char.ToUpper(c) == c && char.ToLower(c) != c;
        }

        public static bool IsLower(char c)
        {
            return char.ToLower(c) == c && char.ToUpper(c) != c;
        }
	}
}
