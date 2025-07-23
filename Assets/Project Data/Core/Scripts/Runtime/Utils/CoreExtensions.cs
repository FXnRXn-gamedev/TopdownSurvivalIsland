using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FXnRXn
{
	public static class CoreExtensions
	{
		
		#region Color
		public static Color WithAlpha(Color color, float alpha)
		{
			return new Color(color.r, color.g, color.b, alpha);
		}
		#endregion

	}
}
