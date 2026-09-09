using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace booking;

internal class lakewood : club
{
	private int index = -1;

	private string targetPath;

	public lakewood(ChromeDriver drv_p, string id_p, string pwd_p, string date_p, int start, int end, int course_p)
	{
		date = date_p;
		startTime = start;
		endTime = end;
		fails = new Dictionary<string, int>();
		teeInterval = 7;
		howManyTees(1138, 739).ToString();
	}

	public override bool login(Form1 _frm)
	{
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		frm = _frm;
		((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5L);
		((WebDriver)drv).Navigate().GoToUrl("https://lakewood.co.kr/member/login?returnURL=/reservation/golf");
		((WebDriver)drv).FindElement(By.XPath("//*[@id='usrId']")).SendKeys(id);
		((WebDriver)drv).FindElement(By.XPath("//*[@id='usrPwd']")).SendKeys(pwd);
		((WebDriver)drv).FindElement(By.XPath("//*[@id='fnLogin']")).Click();
		((DefaultWait<IWebDriver>)new WebDriverWait((IWebDriver)(object)drv, new TimeSpan(0, 0, 30))).Until<IAlert>(ExpectedConditions.AlertIsPresent());
		Thread.Sleep(100);
		try
		{
			string text = WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
			if (text != null && text.Contains("아이디 혹은"))
			{
				frm.logtxtBox(" Login Failed " + text);
				return false;
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T #" + threadIndex + "Login Error " + 0 + " Msg" + ex);
			return false;
		}
		Thread.Sleep(100);
		int row = 0;
		int col = 0;
		int table = 0;
		date = bookReqs[0].date;
		GetPosDate(date, ref table, ref row, ref col);
		targetPath = "//*[@id='calendarDiv']/div[" + table + "]/table/tbody/tr[" + row + "]/td[" + col + "]/a";
		return true;
	}

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement result = null;
		string[] array = tee.GetAttribute("onclick").Split(',');
		int num = int.Parse(array[1].Replace("'", ""));
		if (num < req.startTime)
		{
			teeIndex++;
		}
		else if (num > req.endTime)
		{
			teeIndex--;
		}
		else if (req.starter == "NA" || req.starter == array[3].Replace("'", ""))
		{
			result = tee;
		}
		return result;
	}

	private void test()
	{
		_ = ((ISearchContext)((ISearchContext)((WebDriver)drv).FindElement(By.XPath("//*[@id='calendar_view_ajax_1']/table/tbody"))).FindElement(By.XPath(".//tr[2]/td[5]/a"))).FindElement(By.XPath(".//span")).Text;
	}

	private int howManyTees(int startTime, int firstTime)
	{
		if (startTime <= firstTime)
		{
			return 0;
		}
		DateTime dateTime = new DateTime(2022, 1, 15, startTime / 100, startTime % 100, 0);
		DateTime dateTime2 = new DateTime(2022, 1, 15, firstTime / 100, firstTime % 100, 0);
		return (int)(dateTime - dateTime2).TotalMinutes / teeInterval;
	}

	public override OpResult bookOne()
	{
		try
		{
			((IWebDriver)(object)drv).FindElement(By.XPath(targetPath)).Click();
			Thread.Sleep(100);
			ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='tabCourseALL']/div[2]/div[1]/table/tbody/tr"));
			if (readOnlyCollection == null)
			{
				return OpResult.NoSpace;
			}
			if (readOnlyCollection.Count == 1 && ((ISearchContext)readOnlyCollection[0]).FindElement(By.TagName("td")).Text.StartsWith("Tee-off"))
			{
				return OpResult.NoSpace;
			}
			string starter = bookReqs[0].starter;
			frm.logtxtBox("Availables slots " + readOnlyCollection.Count + " index " + index);
			int count = readOnlyCollection.Count;
			if (index < 0)
			{
				index = ((count > 30) ? (count / 4) : (count / 3));
			}
			else if (index >= count)
			{
				index = count - 1;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			int num = 0;
			while (true)
			{
				if (dictionary.ContainsKey(index) || index < 0 || index >= readOnlyCollection.Count)
				{
					frm.logtxtBox("시간에 맞는 자리 없음");
					break;
				}
				dictionary.Add(index, 0);
				ReadOnlyCollection<IWebElement> readOnlyCollection2 = ((ISearchContext)readOnlyCollection[index]).FindElements(By.TagName("td"));
				try
				{
					int num2 = int.Parse(readOnlyCollection2[2].Text.Replace(":", ""));
					if (num2 < startTime)
					{
						num = 1;
						index++;
						continue;
					}
					if (num2 > endTime)
					{
						num = -1;
						index--;
						continue;
					}
					frm.logtxtBox("Time Okay " + num2);
					if (!starter.Contains("NA") && !starter.Contains(readOnlyCollection2[1].Text))
					{
						if (num >= 0)
						{
							index++;
						}
						else
						{
							index--;
						}
						continue;
					}
					IWebElement val = ((ISearchContext)readOnlyCollection2[4]).FindElement(By.TagName("button"));
					if (((ISearchContext)val).FindElement(By.XPath(".//span")).Text == "신청")
					{
						if (tryReserve(drv, val))
						{
							frm.logtxtBox("성공 : " + num2);
							return OpResult.Success;
						}
					}
					else
					{
						frm.logtxtBox("button shows No 신청");
					}
					frm.logtxtBox("Find one But failed " + num2);
					return OpResult.ReserveFail;
				}
				catch (Exception ex)
				{
					frm.logtxtBox(DateTime.Now.ToString() + " index teeoff time crashed or stale element " + index + "  " + ex);
					break;
				}
			}
		}
		catch (Exception ex2)
		{
			frm.logtxtBox("Trying Error by Thread #" + Task.CurrentId + "  " + ex2);
		}
		return OpResult.Fail;
	}

	private bool tryReserve(ChromeDriver drv, IWebElement btn)
	{
		try
		{
			btn.Click();
			Thread.Sleep(100);
			string text = ((IWebDriver)(object)drv).FindElement(By.Id("golfTimeDiv2CertNo")).Text;
			((WebDriver)drv).FindElement(By.Id("certNoChk")).SendKeys(text);
			((WebDriver)drv).FindElement(By.XPath("//*[@id='golfTimeDiv2']/div[3]/div/div[1]/button")).Click();
			Thread.Sleep(300);
			string text2 = ((WebDriver)drv).SwitchTo().Alert().Text;
			((WebDriver)drv).SwitchTo().Alert().Accept();
			if (!text2.Contains("동반자등록"))
			{
				frm.logtxtBox("Clicked But Failed" + text2);
				return false;
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("Click but Reservation Failed " + ex);
			try
			{
				((WebDriver)drv).SwitchTo().Alert().Accept();
			}
			catch
			{
				frm.logtxtBox("Alert accept fail " + ex);
			}
			return false;
		}
		return true;
	}

	private bool isDialogPresent(WebDriver driver)
	{
		return ExpectedConditions.AlertIsPresent()((IWebDriver)(object)driver) != null;
	}

	public override bool monitorOne(string date_p)
	{
		return false;
	}
}
