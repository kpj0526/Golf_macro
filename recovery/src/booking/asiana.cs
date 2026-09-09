#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Tesseract;

namespace booking;

internal class asiana : club
{
	private TesseractEngine engine;

	private string dateLink;

	private const string loginUrl = "https://www.asianacc.co.kr:444/";

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	public override void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		base.setValues(drv_p, threadIndex_p, cd, bookReqs_p, ref _workPool);
		string text = "tessdata";
		try
		{
			Trace.WriteLine("engine Init");
			if (Directory.Exists(text))
			{
				engine = new TesseractEngine(text, "eng", (EngineMode)3);
				if (engine != null)
				{
					engine.SetVariable("tessedit_char_whitelist", "0123456789");
					Trace.WriteLine("engine Okay");
				}
				else
				{
					frm.logtxtBox("Tesseract init failed");
				}
			}
			else
			{
				frm.logtxtBox("No Tessdata Directory");
			}
			if (engine == null)
			{
				Thread.Sleep(100000);
				Environment.Exit(99);
			}
		}
		catch (Exception ex)
		{
			Trace.WriteLine("engine Error" + ex);
			Thread.Sleep(100000);
			Environment.Exit(99);
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
			((WebDriver)drv).Navigate().GoToUrl("https://www.asianacc.co.kr:444/");
			Thread.Sleep(500);
			((WebDriver)drv).SwitchTo().Frame("bottom");
			((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[1]/a[2]")).Click();
			string currentWindowHandle = ((WebDriver)drv).CurrentWindowHandle;
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='id_012']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='id_013']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[3]/td[2]/table/tbody/tr[2]/td[2]/table/tbody/tr[1]/td[3]/a")));
				Thread.Sleep(500);
				_ = ((WebDriver)drv).WindowHandles.Count;
				((WebDriver)drv).SwitchTo().Window(currentWindowHandle);
				Thread.Sleep(200);
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
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " handleAlert " + ex);
			return result;
		}
	}

	private string selectDate(string date)
	{
		_ = CultureInfo.CurrentCulture.Calendar;
		int row = 0;
		int col = 0;
		int table = 0;
		string text = null;
		bool flag = false;
		GetPosDate(date, ref table, ref row, ref col);
		string text2 = int.Parse(date.Substring(4, 2)).ToString();
		for (int i = 0; i < 3; i++)
		{
			if (((WebDriver)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[3]/td[2]/table/tbody/tr/td/table/tbody/tr[2]/td/div[1]")).Text.Contains(text2 + "월"))
			{
				flag = true;
				break;
			}
			((WebDriver)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[3]/td[2]/table/tbody/tr/td/table/tbody/tr[2]/td/div[1]/a[2]")).Click();
		}
		if (!flag)
		{
			frm.logtxtBox("Month Not Found");
			return null;
		}
		Thread.Sleep(30);
		if (dateLink != null)
		{
			return dateLink;
		}
		for (int j = 0; j < 5; j++)
		{
			try
			{
				string text3 = "";
				text = $"/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[3]/td[2]/table/tbody/tr/td/table/tbody/tr[2]/td/table[2]/tbody/tr/td/table/tbody/tr[{row + j}]/td[{col}]/";
				try
				{
					text3 = ((WebDriver)drv).FindElement(By.XPath(text + "p")).Text;
				}
				catch
				{
					try
					{
						text3 = ((WebDriver)drv).FindElement(By.XPath(text + "a")).Text;
						goto end_IL_0112;
					}
					catch (Exception ex)
					{
						frm.logtxtBox("date NOT FOUND -----------------------No link \n" + ex.Message);
					}
					goto end_IL_00cd;
					end_IL_0112:;
				}
				if (int.Parse(text3.Split("\n")[0].Trim()) == int.Parse(date.Substring(6, 2)))
				{
					if (text3.Contains("마감"))
					{
						frm.logtxtBox("예약마감");
						return "마감";
					}
					break;
				}
				text = null;
				end_IL_00cd:;
			}
			catch (Exception ex2)
			{
				frm.logtxtBox("find Date link No link \n" + ex2.Message);
			}
		}
		dateLink = text;
		return dateLink;
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
		Thread.Sleep(30);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		string currentWindowHandle = ((WebDriver)drv).CurrentWindowHandle;
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				((WebDriver)drv).SwitchTo().Window(currentWindowHandle);
				((WebDriver)drv).SwitchTo().Frame("bottom");
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[1]/table/tbody/tr[2]/td/a"));
				val.Click();
				string text = selectDate(bookInfo2.date);
				if ("마감" == text)
				{
					return OpResult.NoSpace;
				}
				if (text == null)
				{
					frm.logtxtBox("date Link Not Found");
					return OpResult.Fail;
				}
				try
				{
					val = ((WebDriver)drv).FindElement(By.XPath(text + "a"));
				}
				catch (Exception)
				{
					frm.logtxtBox("No Open");
					goto end_IL_00ad;
				}
				_ = DateTime.Now;
				val.Click();
				Thread.Sleep(30);
				IWebElement val2 = findOne(bookInfo2);
				if (val2 != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					opResult = tryReserve(val2);
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
				end_IL_00ad:;
			}
			catch (Exception ex2)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex2);
				workPool.returnDay(num);
				break;
			}
		}
		return OpResult.Fail;
	}

	private IWebElement findOne(bookInfo req)
	{
		string text = "";
		IWebElement result = null;
		try
		{
			foreach (IWebElement item in ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@class='btns reservation_btn']"), 5))
			{
				string attribute = item.GetAttribute("href");
				int num = attribute.IndexOf("time");
				int num2 = int.Parse(attribute.Substring(num + 5, 4));
				if (num2 >= req.startTime && num2 <= req.endTime)
				{
					result = item;
					break;
				}
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # find fail " + text + " Msg " + ex);
		}
		return result;
	}

	private string captchaParse()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='noselect']"));
		if (val == null)
		{
			frm.logtxtBox("T # " + threadIndex + " no captcha");
			return null;
		}
		try
		{
			Mat obj = BitmapConverter.ToMat(Image.FromStream(new MemoryStream(((EncodedFile)((ITakesScreenshot)val).GetScreenshot()).AsByteArray)) as Bitmap);
			new Mat();
			new Mat();
			string text = DateTime.Now.ToString("yyyyMMddHHmmss") + "1.bmp";
			obj.SaveImage(text, (int[])null);
			Bitmap bitmap = BitmapConverter.ToBitmap(obj);
			MemoryStream memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
			engine.SetVariable("tessedit_char_whitelist", "0123456789");
			Pix val2 = Pix.LoadFromMemory(memoryStream.ToArray());
			try
			{
				Page val3 = engine.Process(val2, (PageSegMode?)null);
				try
				{
					return val3.GetText();
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " found slot and trying to book " + ex);
		}
		return null;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			_ = ((WebDriver)drv).CurrentWindowHandle;
			btn.Click();
			Thread.Sleep(30);
			Thread.Sleep(100);
			while (true)
			{
				string text = captchaParse();
				if (text == null)
				{
					return result;
				}
				frm.logtxtBox("T # " + threadIndex + " number " + text);
				text = Regex.Match(text, "\\d+").Value;
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='txtCaptcha']"));
				val.SendKeys(text);
				IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[3]/td[2]/form/table/tbody/tr[4]/td/p/a"));
				if (val2 == null)
				{
					frm.logtxtBox("no button");
					return result;
				}
				WebDriverExtensions.clickLock(val2);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				Thread.Sleep(100);
				string text2 = handleAlert();
				if (text2 != null)
				{
					if (!text2.Contains("보안문자"))
					{
						result = OpResult.Fail;
						return result;
					}
					frm.logtxtBox("보안문자 오류 " + text);
					val.Clear();
				}
				else
				{
					if (((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/table/tbody/tr/td[3]/table/tbody/tr[1]/td/img")).GetAttribute("alt").Contains("예약현황"))
					{
						break;
					}
					frm.logtxtBox("T # Failed !!!!!");
				}
			}
			frm.logtxtBox("T # " + threadIndex + " 예약 성공 !!!!!");
			result = OpResult.BookOneSuccess;
		}
		catch (Exception ex)
		{
			frm.logtxtBox("thread # " + threadIndex + " Reservation Buttone click, but Failed " + ex);
			handleAlert();
		}
		return result;
	}
}
