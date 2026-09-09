using System;
using System.Collections.ObjectModel;
using System.Threading;
using OpenQA.Selenium;
using Tesseract;

namespace booking;

internal class seowon : club
{
	private IWebElement target;

	private TesseractEngine engine;

	private const string loginUrl = "https://www.seowongolf.co.kr/hills/member/login.do";

	public seowon()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		try
		{
			engine = new TesseractEngine("../../../tessdata", "eng", (EngineMode)3);
			engine.SetVariable("tessedit_char_whitelist", "0123456789");
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + "engine " + ex);
		}
	}

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	private bool buildTeeTable()
	{
		int num = 20;
		for (int i = 0; i < bookReqs.Count; i++)
		{
			indexTeeTable[i] = num;
		}
		return true;
	}

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement result = null;
		try
		{
			ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)tee).FindElements(By.TagName("td"));
			frm.logtxtBox(readOnlyCollection[3].Text + "," + readOnlyCollection[4].Text + " ," + readOnlyCollection[9].Text + " index " + teeIndex);
			int num = int.Parse(readOnlyCollection[3].Text.Split(" ")[1].Replace(":", ""));
			if (searchStartTime == 0)
			{
				searchStartTime = num;
			}
			if (num < req.startTime)
			{
				teeIndex++;
				increment = 1;
			}
			else if (num > req.endTime)
			{
				teeIndex--;
				increment = -1;
			}
			else
			{
				teeIndex += increment;
				result = ((ISearchContext)readOnlyCollection[9]).FindElement(By.XPath(".//*[@class='orangeBtn']"));
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + "compare " + ex);
		}
		return result;
	}

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		try
		{
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.seowongolf.co.kr/hills/member/login.do");
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='msId']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='msPw']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='loginBtn']")));
				Thread.Sleep(1500);
				try
				{
					if (((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='topLogin']")) == null)
					{
						frm.logtxtBox("T # " + threadIndex + "login fail ");
						return false;
					}
					Thread.Sleep(200);
					((WebDriver)drv).ExecuteScript("window.scrollTo(500, 200)", Array.Empty<object>());
					((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='main_wrap']/div[2]/div/div[1]/div/ul/li[2]/a"));
				}
				catch (Exception)
				{
				}
				Thread.Sleep(500);
				buildTeeTable();
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	private void handleAlert()
	{
		try
		{
			((WebDriver)drv).SwitchTo().Alert().Accept();
		}
		catch (Exception)
		{
		}
	}

	private (bool, string) setDatePath(string dateP)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		bool posDate = GetPosDate(dateP, ref table, ref row, ref col);
		string item = "/html/body/div[26]/div/div[6]/div[3]/div[1]/" + $"div[{table.ToString()}]/table/tbody[2]/tr[{row.ToString()}]/td[{col.ToString()}]";
		return (posDate, item);
	}

	public override OpResult bookOne()
	{
		int num = -1;
		int num2 = openTimeCheck();
		if (num2 > 5 && !frm.testMode)
		{
			frm.logtxtBox("T # Long Sleep " + num2);
			Thread.Sleep(10000);
			return OpResult.NotOpen;
		}
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		Thread.Sleep(30);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		string item;
		try
		{
			item = setDatePath(bookInfo2.date).Item2;
			Thread.Sleep(20);
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " : GoToUrl Error " + bookInfo2.course + " " + bookInfo2.date + " Msg" + ex);
			handleAlert();
			workPool.returnDay(num);
			return OpResult.Fail;
		}
		_ = DateTime.Now;
		int num3 = 0;
		while (num >= 0 && !frm.stopClicked)
		{
			((WebDriver)drv).Navigate().GoToUrl("https://www.seowongolf.co.kr/hills/reservation/reservation.do ");
			Thread.Sleep(200);
			try
			{
				((WebDriver)drv).FindElement(By.ClassName("closeBox")).Click();
			}
			catch (Exception ex2)
			{
				frm.logtxtBox("CloseBox Error : " + ex2.Message);
			}
			try
			{
				frm.logtxtBox("T # " + threadIndex + " day index " + bookInfo2?.ToString() + " cnt " + num3++);
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(item));
				if (target == null)
				{
					frm.logtxtBox("No date link");
					continue;
				}
				switch (target.GetAttribute("class"))
				{
				case null:
				case "beforeOpen":
					frm.logtxtBox("T # " + threadIndex + " beforeOpen");
					continue;
				case "deadLine":
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + " 마감 다음 예약 이동");
					return OpResult.BookOneSuccess;
				}
				target.Click();
				Thread.Sleep(200);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("/html/body/div[26]/div/div[6]/table/tbody[2]/tr"));
				if (readOnlyCollection == null)
				{
					continue;
				}
				if (readOnlyCollection.Count == 0)
				{
					return OpResult.NoSpace;
				}
				IWebElement val = find(readOnlyCollection, bookInfo2);
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					opResult = tryReserve(val);
					if (opResult == OpResult.BookOneSuccess || opResult == OpResult.DuplicateFail || opResult == OpResult.DuplicateLogin)
					{
						workPool.finishDay(num);
					}
					else
					{
						workPool.returnDay(num);
					}
				}
				else
				{
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + "NoProperTime");
				}
				break;
			}
			catch (Exception ex3)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex3);
				workPool.returnDay(num);
				break;
			}
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			btn.Click();
			Thread.Sleep(300);
			handleAlert();
			frm.logtxtBox("Almost Done");
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[6]/div[2]/a[1]"));
			Thread.Sleep(100);
			if (val == null)
			{
				frm.logtxtBox("no button");
				return result;
			}
			try
			{
				WebDriverExtensions.clickLock(val);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				Thread.Sleep(300);
				string text = ((WebDriver)drv).SwitchTo().Alert().Text;
				((WebDriver)drv).SwitchTo().Alert().Accept();
				if (!text.Contains("예약이 완료"))
				{
					frm.logtxtBox("실패 " + text);
					result = OpResult.Fail;
					return result;
				}
				frm.logtxtBox("T # " + threadIndex + "---- 예약 완료 ----" + text);
				result = OpResult.BookOneSuccess;
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T # " + threadIndex + " found slot and trying to book " + ex);
				handleAlert();
			}
		}
		catch (Exception ex2)
		{
			frm.logtxtBox("thread # " + threadIndex + " Reservation Buttone click, but Failed " + ex2);
			handleAlert();
		}
		return result;
	}
}
