#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Tesseract;

namespace booking;

internal class rexfield : club
{
	private IWebElement target;

	private TesseractEngine engine;

	private const string loginUrl = "https://www.rexfield.com/login/login.asp";

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	public override void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		base.setValues(drv_p, threadIndex_p, cd, bookReqs_p, ref _workPool);
		string text = "tessdata";
		try
		{
			Trace.WriteLine("engine Init");
			if (!Directory.Exists(text))
			{
				Trace.WriteLine("No Tessdata Directory");
				return;
			}
			engine = new TesseractEngine(text, "eng", (EngineMode)3);
			engine.SetVariable("tessedit_char_whitelist", "0123456789");
			Trace.WriteLine("engine Okay");
		}
		catch (Exception ex)
		{
			Trace.WriteLine("engine Error" + ex);
		}
	}

	private bool buildTeeTable()
	{
		int num = 70;
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
			frm.logtxtBox(readOnlyCollection[0].Text + "," + readOnlyCollection[1].Text + " ," + readOnlyCollection[5].Text + " index " + teeIndex);
			int num = int.Parse(readOnlyCollection[1].Text.Replace(":", ""));
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
				if (readOnlyCollection[5].Text == "예약하기" && (req.starter == "NA" || req.starter == readOnlyCollection[0].Text))
				{
					result = readOnlyCollection[5];
				}
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
			((WebDriver)drv).Navigate().GoToUrl("https://www.rexfield.com/login/login.asp");
			Thread.Sleep(500);
			_ = ((WebDriver)drv).CurrentWindowHandle;
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='log_id']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='login_pw']")).SendKeys(pwd);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='login_birth']")).SendKeys("134111");
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='bt_login']")));
				Thread.Sleep(500);
				Thread.Sleep(200);
				try
				{
					handleAlert();
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + " : Course click Error " + 0 + " Msg" + ex);
					return false;
				}
				buildTeeTable();
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	private string handleAlert()
	{
		string result = null;
		try
		{
			IAlert obj = ((WebDriver)drv).SwitchTo().Alert();
			result = obj.Text;
			obj.Accept();
			return result;
		}
		catch (Exception)
		{
			return result;
		}
	}

	private string selectDate(string date)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		GetPosDate(date, ref table, ref row, ref col);
		return "//*[@id='calendar_view_ajax_" + table + "']/div[1]/div[3]/table/tbody/tr[" + row + "]/td[" + col + "]/div/div/div/div/a";
	}

	public override OpResult bookOne()
	{
		int num = -1;
		int num2 = openTimeCheck();
		if (num2 > 3 && !frm.testMode)
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
		string text = selectDate(bookInfo2.date);
		string text2 = selectDate((int.Parse(bookInfo2.date) + 1).ToString());
		((WebDriver)drv).Navigate().GoToUrl("https://www.rexfield.com/GolfRes/onepage/real_reservation.asp");
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				string text3 = text[..text.LastIndexOf("]")] + "]";
				if (((WebDriver)drv).FindElement(By.XPath(text3)).GetAttribute("onmouseover").Contains("오픈전"))
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).Navigate().Refresh();
					continue;
				}
				IWebElement val = ((WebDriver)drv).FindElement(By.XPath(text));
				Thread.Sleep(50);
				val.Click();
				Thread.Sleep(70);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((WebDriver)drv).FindElements(By.XPath("//img[contains(@alt,'신청')]"));
				if (readOnlyCollection.Count == 0)
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).FindElement(By.XPath(text2)).Click();
					continue;
				}
				foreach (IWebElement item in readOnlyCollection)
				{
					int num3 = int.Parse(item.GetAttribute("alt").Substring(3, 4));
					if (num3 >= bookInfo2.startTime && num3 <= bookInfo2.endTime)
					{
						val = item;
						break;
					}
				}
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
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
				workPool.returnDay(num);
			}
			break;
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			btn.Click();
			Thread.Sleep(100);
			string text = handleAlert();
			if (text == null)
			{
				frm.logtxtBox("tryReserve Click Failed with NULL !!!!!");
			}
			if (text.Contains("예약이 완료"))
			{
				frm.logtxtBox("예약 성공 !!!!!");
				result = OpResult.BookOneSuccess;
			}
			else
			{
				frm.logtxtBox("tryReserve Click Failed with " + text);
			}
		}
		catch (Exception ex)
		{
			string text2 = handleAlert();
			string text3 = "";
			if (text2 != null)
			{
				text3 = text2;
			}
			frm.logtxtBox(" Reservation Buttone click, but Failed " + text3 + ":" + ex);
		}
		return result;
	}
}
