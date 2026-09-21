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
	// Selenium waits poll every 500 ms by default.  At a reservation opening that
	// default alone can lose a tee time after the site has already responded.
	private const int FastWaitPollMilliseconds = 10;

	public static object lockObject = new object();

	public static int delay1 = 100;

	public static int threadNum = 1;

	public static Form1 frm;

	private static WebDriverWait FastWait(IWebDriver driver, TimeSpan timeout)
	{
		var wait = new WebDriverWait(driver, timeout);
		wait.PollingInterval = TimeSpan.FromMilliseconds(FastWaitPollMilliseconds);
		return wait;
	}

	public static void clickableClick(this IWebDriver driver, IWebElement ele, int timeoutInSeconds = 1)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		try
		{
			FastWait(driver, TimeSpan.FromSeconds(timeoutInSeconds)).Until<IWebElement>(ExpectedConditions.ElementToBeClickable(ele)).Click();
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
			return (timeoutInSeconds <= 0) ? ((ISearchContext)driver).FindElement(by) : FastWait(driver, TimeSpan.FromSeconds(timeoutInSeconds)).Until<IWebElement>((Func<IWebDriver, IWebElement>)((IWebDriver drv) => ((ISearchContext)drv).FindElement(by)));
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
			return (timeoutInSeconds <= 0) ? ((ISearchContext)driver).FindElements(by) : FastWait(driver, TimeSpan.FromSeconds(timeoutInSeconds)).Until<ReadOnlyCollection<IWebElement>>((Func<IWebDriver, ReadOnlyCollection<IWebElement>>)((IWebDriver drv) => (((ISearchContext)drv).FindElements(by).Count <= 0) ? null : ((ISearchContext)drv).FindElements(by)));
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

	// Retries a click a bounded number of times, tolerating the transient states a click
	// can hit while a page is still rendering/transitioning right at a reservation's
	// opening moment: ElementNotInteractableException, ElementClickInterceptedException,
	// and StaleElementReferenceException (the DOM was re-rendered under load between
	// lookup and click). `locate` is called fresh before every attempt so a stale
	// reference is re-resolved instead of retried as-is.
	public static bool ClickWithRetry(Func<IWebElement> locate, int maxAttempts = 4, int delayMs = 10)
	{
		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				IWebElement element = locate();
				if (element != null)
				{
					element.Click();
					return true;
				}
			}
			// ElementClickInterceptedException derives from ElementNotInteractableException
			// in this Selenium client, so one catch covers both.
			catch (ElementNotInteractableException) { }
			catch (StaleElementReferenceException) { }
			catch (NoSuchElementException) { }
			if (attempt < maxAttempts)
			{
				Thread.Sleep(delayMs);
			}
		}
		return false;
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
