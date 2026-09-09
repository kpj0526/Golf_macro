using System;
using System.Collections.ObjectModel;
using System.Threading;
using OpenQA.Selenium;

namespace booking;

internal class newSpring : club
{
	private IWebElement target;

	private string targetPath;

	public newSpring(int pManagerId)
	{
		managerId = pManagerId;
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

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		try
		{
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5L);
			((WebDriver)drv).Navigate().GoToUrl("https://icheon.newspring.co.kr/Member/Login?url=L0Jvb2tpbmcvR29sZkNhbGVuZGFy");
			IWebElement val = ((WebDriver)drv).FindElement(By.XPath("//*[@id='id']"));
			val.SendKeys(id);
			val = ((WebDriver)drv).FindElement(By.XPath("//*[@id='pwd']"));
			val.SendKeys(pwd);
			val = ((WebDriver)drv).FindElement(By.XPath("//*[@class='loginBtn']"));
			val.Click();
			Thread.Sleep(100);
			try
			{
				val = ((WebDriver)drv).FindElement(By.Id("alertMsg"));
				if (val.Text.Contains("비밀번호"))
				{
					frm.logtxtBox("thread # " + threadIndex + " Login Failed" + val.Text);
					return false;
				}
			}
			catch (Exception)
			{
			}
			int row = 0;
			int col = 0;
			int table = 0;
			GetPosDate(bookReqs[0].date, ref table, ref row, ref col);
			targetPath = "//*[@id='golf_calendar']/div[" + (table + 1) + "]/table/tbody/tr[" + row + "]/td[" + col + "]/a";
			startTime = bookReqs[0].startTime;
			endTime = bookReqs[0].endTime;
			course = bookReqs[0].course;
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	public override OpResult bookOne()
	{
		if (!frm.stopClicked)
		{
			try
			{
				((WebDriver)drv).Navigate().Refresh();
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(targetPath), 10);
				if (!target.GetAttribute("class").Contains("reserved"))
				{
					return OpResult.NotOpen;
				}
				target.Click();
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='AjaxTime']"), 15)).FindElements(By.ClassName("timeTbl"));
				for (int i = 0; i < 4; i++)
				{
					if (course >= 4)
					{
						course = 0;
					}
					ReadOnlyCollection<IWebElement> readOnlyCollection2 = ((ISearchContext)readOnlyCollection[course]).FindElements(By.XPath(".//tbody/tr"));
					frm.logtxtBox("Available " + readOnlyCollection2.Count);
					foreach (IWebElement item in readOnlyCollection2)
					{
						ReadOnlyCollection<IWebElement> readOnlyCollection3 = ((ISearchContext)item).FindElements(By.XPath("td"));
						int num = checkTime(readOnlyCollection3[0].Text.Replace(":", ""), startTime, endTime);
						if (num == 0)
						{
							frm.logtxtBox("will try " + readOnlyCollection3[0].Text);
							OpResult opResult = tryReserve(readOnlyCollection3[2]);
							switch (opResult)
							{
							case OpResult.Success:
								return OpResult.Success;
							case OpResult.Overbook:
								return opResult;
							}
							frm.logtxtBox("Trying But failed " + readOnlyCollection3[0].Text);
						}
						else if (num == 2 || num < 0)
						{
							break;
						}
					}
					course++;
				}
				return OpResult.NoProperTime;
			}
			catch (Exception)
			{
				return OpResult.Fail;
			}
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		try
		{
			btn.Click();
			Thread.Sleep(10);
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='mediumBtn']/a[2]"), 5);
			if (val == null)
			{
				return OpResult.Overbook;
			}
			if (!(val.GetAttribute("title") == "예약"))
			{
				return OpResult.ReserveFail;
			}
			val.Click();
			Thread.Sleep(100);
			((WebDriver)drv).SwitchTo().Alert().Accept();
			Thread.Sleep(1000);
			((WebDriver)drv).SwitchTo().Alert().Accept();
		}
		catch (Exception)
		{
			return OpResult.ReserveFail;
		}
		return OpResult.Success;
	}

	public override bool monitorOne(string date_p)
	{
		try
		{
			string attribute = ((WebDriver)drv).FindElement(By.XPath("//*[@id='" + date_p + "']//a")).GetAttribute("title");
			if (!attribute.Contains("마감") && !attribute.Contains("오픈전"))
			{
				return true;
			}
		}
		catch (Exception)
		{
			return false;
		}
		return false;
	}
}
