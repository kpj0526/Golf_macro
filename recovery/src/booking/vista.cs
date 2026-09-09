using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenQA.Selenium;
using Tesseract;

namespace booking;

internal class vista : club
{
	private IWebElement target;

	private TesseractEngine engine;

	private const string loginUrl = "https://www.bavista.co.kr/Member/Login?url=L0Jvb2tpbmcvR29sZkNhbGVuZGFy";

	public vista()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		try
		{
			engine = new TesseractEngine("tessdata", "eng", (EngineMode)3);
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
			((WebDriver)drv).Navigate().GoToUrl("https://www.bavista.co.kr/Member/Login?url=L0Jvb2tpbmcvR29sZkNhbGVuZGFy");
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='id']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='pwd']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='loginBtn']")));
				Thread.Sleep(500);
				try
				{
					string text = ((WebDriver)drv).SwitchTo().Alert().Text;
					if (!text.Contains("환영"))
					{
						frm.logtxtBox("T # " + threadIndex + "login fail " + text);
						return false;
					}
					((WebDriver)drv).SwitchTo().Alert().Accept();
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

	private string selectDate(string date)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		GetPosDate(date, ref table, ref row, ref col);
		return $"//*[@id='golf_calendar']/div[{table}]/table/tbody/tr[{row}]/td[{col}]/a";
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
		Thread.Sleep(30);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		string text;
		try
		{
			text = selectDate(bookInfo2.date);
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
		if (frm.testMode)
		{
			((WebDriver)drv).Navigate().GoToUrl("https://www.bavista.co.kr/Booking/GolfCalendar?coGubun=P");
		}
		else
		{
			((WebDriver)drv).Navigate().GoToUrl("https://www.bavista.co.kr/Booking/GolfCalendar?coGubun=M");
		}
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				frm.logtxtBox("T # " + threadIndex + " day index " + bookInfo2?.ToString() + " cnt " + num3++);
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 5);
				if (target == null || !target.GetAttribute("class").Contains("reserve"))
				{
					frm.logtxtBox("Not open");
					((WebDriver)drv).Navigate().Refresh();
					Thread.Sleep(100);
					continue;
				}
				if (target.GetAttribute("data-cnt") == null)
				{
					if (target.GetAttribute("class") == "closed day")
					{
						workPool.finishDay(num);
						frm.logtxtBox("T # " + threadIndex + " 마감 다음 예약 이동");
						return OpResult.BookOneSuccess;
					}
					continue;
				}
				WebDriverExtensions.clickLock(target);
				if (bookInfo2.course > 0)
				{
					Thread.Sleep(50);
				}
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='AjaxTime']/div/div[2]/table/tbody/tr"));
				if (bookInfo2.course > 0)
				{
					Thread.Sleep(50);
				}
				if (readOnlyCollection == null)
				{
					break;
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
			}
			catch (Exception ex2)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex2);
			}
			break;
		}
		return OpResult.Fail;
	}

	private string captchaParse()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='container']/div[1]/div/div/img"));
		if (val == null)
		{
			frm.logtxtBox("T # " + threadIndex + " no captcha");
			return null;
		}
		try
		{
			Mat val2 = BitmapConverter.ToMat(Image.FromStream(new MemoryStream(((EncodedFile)((ITakesScreenshot)val).GetScreenshot()).AsByteArray)) as Bitmap);
			new Mat();
			new Mat();
			Cv2.Resize((InputArray)(val2), (OutputArray)(val2), new OpenCvSharp.Size(0, 0), 2.0, 2.0, (InterpolationFlags)3);
			Cv2.CvtColor((InputArray)(val2), (OutputArray)(val2), (ColorConversionCodes)6, 0);
			Cv2.Threshold((InputArray)(val2), (OutputArray)(val2), 230.0, 255.0, (ThresholdTypes)0);
			Cv2.AdaptiveThreshold((InputArray)(val2), (OutputArray)(val2), 255.0, (AdaptiveThresholdTypes)1, (ThresholdTypes)0, 5, 2.0);
			Bitmap bitmap = BitmapConverter.ToBitmap(val2);
			MemoryStream memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
			Pix val3 = Pix.LoadFromMemory(memoryStream.ToArray());
			try
			{
				Page val4 = engine.Process(val3, (PageSegMode?)null);
				try
				{
					return val4.GetText();
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
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
			btn.Click();
			Thread.Sleep(10);
			string text = captchaParse();
			if (text == null)
			{
				return result;
			}
			frm.logtxtBox("T # " + threadIndex + "number " + text);
			((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='ConfirmNumber']")).SendKeys(text);
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='container']/div[2]/a[2]"));
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
				string text2 = ((WebDriver)drv).SwitchTo().Alert().Text;
				if (!text2.Contains("예약하시겠습니까"))
				{
					result = OpResult.Fail;
					return result;
				}
				((WebDriver)drv).SwitchTo().Alert().Accept();
				Thread.Sleep(300);
				text2 = ((WebDriver)drv).SwitchTo().Alert().Text;
				((WebDriver)drv).SwitchTo().Alert().Accept();
				result = ((text2.Contains("동일한 일자") || text2.Contains("횟수를 초과")) ? OpResult.DuplicateFail : (text2.Contains("예약이 완료") ? OpResult.BookOneSuccess : ((!text2.Contains("다른 곳에서")) ? OpResult.Fail : OpResult.DuplicateLogin)));
				frm.logtxtBox("T # " + threadIndex + " " + text2);
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
