#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Tesseract;

namespace booking;

internal class newKorea : club
{
	private TesseractEngine engine;

	private DateTime opentime;

	private const string loginUrl = "https://www.newkoreacc.co.kr/";

	private string outLink = "/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[1]/a/img";

	private string inLink = "/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[2]/a/img";

	private string refreshLink = "/html/body/table/tbody/tr/td/table/tbody/tr/td[1]/table/tbody/tr[3]/td/table/tbody/tr/td[2]/table/tbody/tr[2]/td/table/tbody/tr[1]/td/a/img";

	public newKorea(int pManagerId)
	{
		managerId = pManagerId;
	}

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
			string text = managerId + "_" + id;
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.newkoreacc.co.kr/");
			Thread.Sleep(500);
			((WebDriver)drv).SwitchTo().Window(((WebDriver)drv).WindowHandles[2]).Close();
			((WebDriver)drv).SwitchTo().Window(((WebDriver)drv).WindowHandles[1]).Close();
			((WebDriver)drv).SwitchTo().Window(((WebDriver)drv).WindowHandles[0]);
			((WebDriver)drv).SwitchTo().Frame("nkcc_main");
			opentime = frm.coreData.getOpenDate(bookReqs[0].date);
			sleepShortToOpen(opentime.AddMinutes(-30.0));
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr/td/table/tbody/tr[3]/td/table/tbody/tr/td[2]/table[1]/tbody/tr/td[1]/table/tbody/tr[1]/td/table/tbody/tr/td[5]/input")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr/td/table/tbody/tr[3]/td/table/tbody/tr/td[2]/table[1]/tbody/tr/td[1]/table/tbody/tr[1]/td/table/tbody/tr/td[7]/input")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr/td/table/tbody/tr[3]/td/table/tbody/tr/td[2]/table[1]/tbody/tr/td[1]/table/tbody/tr[1]/td/table/tbody/tr/td[8]/a/img")));
				Thread.Sleep(200);
				try
				{
					string text2 = handleAlert();
					if (text2 != null && text2.Contains("정보가 틀립니다"))
					{
						frm.logtxtBox("thread # " + threadIndex + " Login Failed" + text2);
						return false;
					}
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + "Login Error " + 0 + " Msg" + ex);
					return false;
				}
				try
				{
					Thread.Sleep(2000);
					foreach (string windowHandle in ((WebDriver)drv).WindowHandles)
					{
						if (!(windowHandle == ((WebDriver)drv).WindowHandles[0]))
						{
							((WebDriver)drv).SwitchTo().Window(windowHandle);
							((WebDriver)drv).Close();
						}
					}
					((WebDriver)drv).SwitchTo().Window(((WebDriver)drv).WindowHandles[0]);
					((WebDriver)drv).SwitchTo().Frame("nkcc_main");
					((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/map[2]/area[1]")).Click();
					Thread.Sleep(30);
				}
				catch (Exception)
				{
				}
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

	private void sleepToOpenBak()
	{
		frm.logtxtBox("Sleep to 00 second");
		string text;
		while (true)
		{
			text = DateTime.Now.ToString("ss.ffff");
			if (text.CompareTo("59.9950") >= 0 || text.CompareTo("01.0300") <= 0)
			{
				break;
			}
			Thread.Sleep(1);
		}
		frm.logtxtBox("expired " + text);
	}

	private string selectDate(string date)
	{
		Calendar calendar = CultureInfo.CurrentCulture.Calendar;
		int num = 0;
		int num2 = 0;
		int day = int.Parse(date.Substring(6, 2));
		int month = int.Parse(date.Substring(4, 2));
		DateTime time = new DateTime(int.Parse(date.Substring(0, 4)), month, day);
		num = calendar.GetWeekOfYear(time, CalendarWeekRule.FirstDay, DayOfWeek.Sunday) - calendar.GetWeekOfYear(DateTime.Today, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
		num++;
		num2 = (int)calendar.GetDayOfWeek(time);
		num2++;
		return $" /html/body/table/tbody/tr/td/table/tbody/tr/td[2]/table/tbody/tr[5]/td/table/tbody/tr[2]/td[2]/table/tbody/tr[2]/td/table/tbody/tr[{num}]/td[{num2}]/table/tbody/tr[2]/td/img";
	}

	public override OpResult bookOne()
	{
		int num = -1;
		int num2 = openTimeCheck();
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		string starter = bookReqs[0].starter;
		string text = selectDate(bookInfo2.date);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		sleepShortToOpen(opentime, 50);
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				((IWebDriver)(object)drv).FindElement(By.XPath(refreshLink)).Click();
				frm.logtxtBox("예약하기 clicked");
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 15);
				string attribute = val.GetAttribute("src");
				if (val == null || !attribute.Contains("icon01"))
				{
					if (attribute.Contains("icon05"))
					{
						frm.logtxtBox("예약 마감");
						workPool.finishDay(num);
						return OpResult.NoSpace;
					}
					frm.logtxtBox("No Open");
					((WebDriver)drv).FindElement(By.XPath(refreshLink)).Click();
					continue;
				}
				val.Click();
				frm.logtxtBox("date clicked");
				Thread.Sleep(100);
				bool flag = true;
				val = null;
				((WebDriver)drv).SwitchTo().Frame("embeded-content");
				if (starter.Contains("IN"))
				{
					((WebDriver)drv).FindElement(By.XPath(inLink)).Click();
					flag = false;
				}
				while (true)
				{
					ReadOnlyCollection<IWebElement> readOnlyCollection = WebDriverExtensions.perfFind((IWebDriver)(object)drv, By.XPath(".//tr[starts-with(@onclick, 'javascript:bookingReg')]"));
					if (readOnlyCollection.Count == 0)
					{
						frm.logtxtBox("No Available");
						((WebDriver)drv).SwitchTo().ParentFrame();
						((WebDriver)drv).FindElement(By.XPath("/html/body/table/tbody/tr/td/table/tbody/tr/td[2]/table/tbody/tr[5]/td/table/tbody/tr[6]/td[2]/table/tbody/tr/td/a/img")).Click();
						((IWebDriver)(object)drv).FindElement(By.XPath(text)).Click();
						((WebDriver)drv).SwitchTo().Frame("embeded-content");
						continue;
					}
					frm.logtxtBox("Available " + readOnlyCollection.Count);
					_ = new char[2] { ' ', '\'' };
					new List<IWebElement>();
					foreach (IWebElement item in readOnlyCollection)
					{
						int num3 = int.Parse(Regex.Replace(item.Text, "\\D", ""));
						if (num3 >= bookInfo2.startTime)
						{
							if (num3 <= bookInfo2.endTime)
							{
								val = item;
							}
							break;
						}
					}
					if (!((val == null) & flag))
					{
						break;
					}
					((WebDriver)drv).FindElement(By.XPath(outLink)).Click();
					frm.logtxtBox("코스 변경 Clicked");
					Thread.Sleep(100);
					flag = false;
				}
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay " + val.Text);
					opResult = tryReserve(val);
					if (opResult == OpResult.BookOneSuccess || opResult == OpResult.DuplicateFail || opResult == OpResult.DuplicateLogin)
					{
						workPool.finishDay(num);
						break;
					}
					workPool.returnDay(num);
					((WebDriver)drv).SwitchTo().ParentFrame();
					((WebDriver)drv).FindElement(By.XPath(refreshLink)).Click();
				}
				else
				{
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + "NoProperTime");
				}
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("예약이 마감"))
				{
					frm.logtxtBox("T #" + threadIndex + " Stop " + ex.Message);
					break;
				}
				workPool.returnDay(num);
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
				((WebDriver)drv).SwitchTo().ParentFrame();
			}
			break;
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult opResult = OpResult.Fail;
		try
		{
			btn.Click();
			Thread.Sleep(10);
			string text = handleAlert();
			if (text.Contains("경기 예약을 하시"))
			{
				Thread.Sleep(100);
				text = handleAlert();
				if (text.Contains("정상적으로 처리되었습니다"))
				{
					frm.logtxtBox("성공했습니다 !!!!! ");
					Thread.Sleep(500000);
					opResult = OpResult.BookOneSuccess;
				}
			}
			if (opResult != OpResult.Success)
			{
				frm.logtxtBox("tryReserve Click Failed with " + text);
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox(" Reservation Buttone click, but Failed :" + ex);
		}
		return opResult;
	}
}
