using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace booking;

public static class WebDriverExtensions
{
	public static object lockObject = new object();

	public static int delay1 = 100;

	public static int threadNum = 1;

	public static Form1 frm;

	public static void clickableClick(this IWebDriver driver, IWebElement ele, int timeoutInSeconds = 1)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		try
		{
			((DefaultWait<IWebDriver>)new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds))).Until<IWebElement>(ExpectedConditions.ElementToBeClickable(ele)).Click();
		}
		catch (Exception ex)
		{
			frm.logtxtBox("IWebElement clickableClick  :" + ex.Message);
		}
	}

	public static IWebElement FindElement(this IWebDriver driver, By by, int timeoutInSeconds = 1)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		IWebElement val = null;
		try
		{
			return (timeoutInSeconds <= 0) ? ((ISearchContext)driver).FindElement(by) : ((DefaultWait<IWebDriver>)new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds))).Until<IWebElement>((Func<IWebDriver, IWebElement>)((IWebDriver drv) => ((ISearchContext)drv).FindElement(by)));
		}
		catch (Exception ex)
		{
			frm.logtxtBox("IWebElement FindElement  :" + ex.Message);
			if (ex.Message.ToString().Contains("예약은 1일 1회") || ex.Message.ToString().Contains("예약 가능 횟수77"))
			{
				MessageBox.Show(ex.Message.ToString());
			}
			return null;
		}
	}

	public static ReadOnlyCollection<IWebElement> FindElements(this IWebDriver driver, By by, int timeoutInSeconds = 1)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		ReadOnlyCollection<IWebElement> readOnlyCollection = null;
		try
		{
			return (timeoutInSeconds <= 0) ? ((ISearchContext)driver).FindElements(by) : ((DefaultWait<IWebDriver>)new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds))).Until<ReadOnlyCollection<IWebElement>>((Func<IWebDriver, ReadOnlyCollection<IWebElement>>)((IWebDriver drv) => (((ISearchContext)drv).FindElements(by).Count <= 0) ? null : ((ISearchContext)drv).FindElements(by)));
		}
		catch (Exception ex)
		{
			frm.logtxtBox("IWebElement FindElements  :" + ex);
			handleAlert(driver);
			return null;
		}
	}

	public static void clickLock(IWebElement ele)
	{
		ele.Click();
		if (threadNum > 1)
		{
			Thread.Sleep(delay1);
		}
	}

	public static string handleAlert(IWebDriver drv)
	{
		string result = null;
		try
		{
			IAlert obj = drv.SwitchTo().Alert();
			result = obj.Text;
			obj.Accept();
		}
		catch (Exception)
		{
		}
		return result;
	}

	public static ReadOnlyCollection<IWebElement> perfFind(IWebDriver drv, By by)
	{
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		ReadOnlyCollection<IWebElement> result = ((ISearchContext)drv).FindElements(by);
		stopwatch.Stop();
		return result;
	}
}
