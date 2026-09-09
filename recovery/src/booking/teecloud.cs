#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Tesseract;

namespace booking;

internal class teecloud : club
{
	private TesseractEngine engine;

	private DateTime opentime;

	private const string loginUrl = "https://www.teecloud.co.kr/new/member/reserve.asp";

	private Random rand = new Random();

	public teecloud(int pManagerId)
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
			string userId = managerId + "_" + id;
			mq = new mqttClient(userId);
			frm.coreData.testServer(mq, 1, frm);
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.teecloud.co.kr/new/member/reserve.asp");
			Thread.Sleep(500);
			((WebDriver)drv).Manage().Window.Maximize();
			Thread.Sleep(1000);
			bookInfo bookInfo2 = bookReqs[0];
			int year = int.Parse(bookInfo2.date.Substring(0, 4));
			int num = int.Parse(bookInfo2.date.Substring(4, 2)) - 1;
			if (int.Parse(bookInfo2.date.Substring(6, 2)) > DateTime.DaysInMonth(year, num))
			{
				num++;
			}
			opentime = frm.coreData.getOpenDate(bookInfo2.date);
			sleepShortToOpen(opentime.AddMinutes(-5.0));
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@name='UserId']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@name='UserPwd']")).SendKeys(pwd);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='content']/div[2]/div/div/div[2]/div/div/div/form/button")).Click();
				Thread.Sleep(200);
				try
				{
					string text = WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
					if (text != null && text.Contains("비밀번호"))
					{
						frm.logtxtBox("thread # " + threadIndex + " Login Failed" + text);
						return false;
					}
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + "Login Error " + 0 + " Msg" + ex);
					return false;
				}
			}
		}
		catch (Exception ex2)
		{
			frm.logtxtBox("Login by Task #" + Task.CurrentId + "  " + ex2);
			return false;
		}
		return true;
	}

	private string selectDate(string date)
	{
		_ = CultureInfo.CurrentCulture.Calendar;
		int row = 0;
		int col = 0;
		int table = 0;
		GetPosDate(date, ref table, ref row, ref col);
		return $"//*[@id='content']/div[3]/div/div/div[2]/div/div/div[1]/div[1]/div[1]/div[{table}]/table/tbody/tr[{row}]/td[{col}]/span/a";
	}

	public override OpResult bookOne()
	{
		int num = -1;
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		string starter = bookReqs[0].starter;
		int indexOfStarter = frm.coreData.getIndexOfStarter(starter);
		string text = selectDate(bookInfo2.date);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		IJavaScriptExecutor val = (IJavaScriptExecutor)(object)drv;
		sleepShortToOpen(opentime, 20);
		((WebDriver)drv).Navigate().GoToUrl("https://www.teecloud.co.kr/new/member/reserve.asp");
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 10);
				if (val2 == null || val2.GetAttribute("title").Contains("예약불가"))
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).Navigate().Refresh();
					Thread.Sleep(100);
					continue;
				}
				val2.Click();
				Thread.Sleep(300);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//a[@href]"), 20);
				new List<(string, IWebElement)>();
				if (readOnlyCollection == null || readOnlyCollection.Count == 0)
				{
					frm.logtxtBox("No Available");
					((WebDriver)drv).Navigate().Refresh();
					continue;
				}
				frm.logtxtBox("Available " + readOnlyCollection.Count);
				new List<IWebElement>();
				(IWebElement, int) tuple = findTime(readOnlyCollection, indexOfStarter, bookInfo2.startTime, bookInfo2.endTime);
				IWebElement item = tuple.Item1;
				course = tuple.Item2;
				course = (course - 48) / 2 + 1;
				string text2 = $"//*[@id='golfTimeDiv']/h5[{course}]/button";
				((IWebDriver)(object)drv).FindElement(By.XPath(text2)).Click();
				if (item != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay " + item.GetAttribute("id"));
					val.ExecuteScript("arguments[0].scrollIntoView(true);", new object[1] { item });
					Thread.Sleep(500);
					opResult = tryReserve(item);
					if (opResult != OpResult.BookOneSuccess && opResult != OpResult.DuplicateFail && opResult != OpResult.DuplicateLogin)
					{
						((WebDriver)drv).Navigate().Refresh();
						Thread.Sleep(200);
						continue;
					}
					workPool.finishDay(num);
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
				}
				else
				{
					workPool.returnDay(num);
					frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
				}
				((WebDriver)drv).Navigate().Refresh();
			}
			break;
		}
		return OpResult.Fail;
	}

	private (IWebElement, int) findTime(ReadOnlyCollection<IWebElement> avails, int starterIndex, int startTime, int endTime)
	{
		IWebElement val = null;
		int num = ((starterIndex != 0) ? starterIndex : rand.Next(1, 3));
		int num2 = 50 + num - 1;
		int num3 = 0;
		for (int i = 0; i < 2; i++)
		{
			if (num3 >= 3)
			{
				break;
			}
			foreach (IWebElement avail in avails)
			{
				string attribute = avail.GetAttribute("id");
				if (attribute[11] < num2)
				{
					continue;
				}
				int num4 = int.Parse(attribute.Substring(12, 4));
				if (num4 >= startTime)
				{
					if (num4 <= endTime)
					{
						val = avail;
						break;
					}
					frm.logtxtBox("Visited Course " + num2);
					num3++;
					if (num2 == 54 || num3 >= 3)
					{
						break;
					}
					num2 += 2;
				}
			}
			if (val != null)
			{
				break;
			}
			num2 = 50;
		}
		return (val, num2);
	}

	private string myhandleAlert(IWebDriver drv)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		string result = null;
		try
		{
			((DefaultWait<IWebDriver>)new WebDriverWait(drv, TimeSpan.FromSeconds(5L))).Until<IAlert>(ExpectedConditions.AlertIsPresent());
			IAlert obj = drv.SwitchTo().Alert();
			result = obj.Text;
			obj.Accept();
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T #  handleAlert " + ex);
		}
		return result;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		OpResult opResult = OpResult.Fail;
		string text = "";
		try
		{
			Thread.Sleep(1000);
			btn.Click();
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[2]/form[2]/div/div/div[3]/div/section[3]/div[2]/ul/li[16]/div/div[2]/span[1]"));
			((IWebDriver)(object)drv).FindElement(By.Id("certNoChk")).SendKeys(val.Text);
			int num = 1267;
			while (num++ < 3000)
			{
				IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='captImg']"));
				if (val2 == null)
				{
					frm.logtxtBox("T # " + threadIndex + " no captcha");
					return opResult;
				}
				_ = val2.Location;
				_ = val2.Size;
				try
				{
					Screenshot screenshot = ((ITakesScreenshot)val2).GetScreenshot();
					string text2 = DateTime.Now.ToString("mmssfff") + "-" + threadIndex + ".png";
					((EncodedFile)screenshot).SaveAsFile(text2);
					if (!frm.coreData.sendCaptcha(mq, text2, frm))
					{
						frm.logtxtBox("Sending Captcha Error");
						return opResult;
					}
					while (mq.waitAck)
					{
						Thread.Sleep(10);
					}
					string receivedMessage = mq.ReceivedMessage;
					frm.logtxtBox("T # " + threadIndex + " 인증번호 " + receivedMessage);
					IWebElement obj = ((IWebDriver)(object)drv).FindElement(By.Id("captAnswer"));
					receivedMessage = (num + 10000).ToString();
					obj.SendKeys(receivedMessage);
					((IWebDriver)(object)drv).FindElement(By.Id("captCheck")).Click();
					myhandleAlert((IWebDriver)(object)drv);
					File.Move(text2, receivedMessage + ".png", overwrite: true);
					((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='btn btn-res03']")).Click();
					Thread.Sleep(300);
					text = ((WebDriver)drv).SwitchTo().Alert().Text;
					((WebDriver)drv).SwitchTo().Alert().Accept();
					Thread.Sleep(30);
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T # " + threadIndex + " found slot and trying to book " + ex);
					myhandleAlert((IWebDriver)(object)drv);
					return opResult;
				}
			}
			text = myhandleAlert((IWebDriver)(object)drv);
			if (text.Contains("예약을 확정"))
			{
				Thread.Sleep(10);
				text = myhandleAlert((IWebDriver)(object)drv);
				if (text != null && text.Contains("정상적으로"))
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
		catch (Exception ex2)
		{
			frm.logtxtBox(" Reservation Buttone click, but Failed :" + ex2);
		}
		return opResult;
	}
}
