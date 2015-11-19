using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Web;



namespace BudNetTrackBaseOnHttpWebRequest
{
    #region  定义json反序列化类
    public class Root
    {
        public D d;
    }
    public class D
    {
        public string type;
        public Group[] Groups;
        public string LastUpdatedDateTimeDisplay;
        public int SecondsUntilNextRefresh;
        public string WholesalerId;

    }
    public class Group
    {
        public string type;
        public Metric[] Metrics;
        public string name;

    }

    public class Metric
    {
        public string type;
        public string DrillDownLocation;
        public string ImageCssClass;
        public string Name;
        public string Value;

    }
    #endregion
    class Program
    {
        public static List<UserInfo> expiredUsers;
        public static List<UserInfo> waitForConfirmed;
        public static List<UserInfo> users;
        public static List<UserInfo> confirmedUsers;
        public static int count = 0;
        public static int processCount = 1;

        static void Main(string[] args)
        {
            //TestProcess();
            ProductionProcess();
        }
        public static void TestProcess()
        {
            users = new List<UserInfo>();
            ReadFileToUsers(users);
            var support = new SupportRequest(users);
            support.LogOnAndAdd();

        }
        public static void ProductionProcess()
        {

            ServicePointManager.DefaultConnectionLimit = 50;
            expiredUsers = new List<UserInfo>();
            waitForConfirmed = new List<UserInfo>();
            confirmedUsers = new List<UserInfo>();
            users = new List<UserInfo>();


            //read bud.txt file to users
            if (!Directory.Exists("\\Result"))
            {
                Directory.CreateDirectory(".\\Result");
            }

            ReadFileToUsers(users);

            //cycle users to requestjson
            BudNetRequest budnetRequest;
            string result = "";
            for (var i = 0; i < users.Count; i++)
            {
                var tempResult = "";
                budnetRequest = new BudNetRequest(users[i]);
                try
                {
                    tempResult = budnetRequest.RequestJson();
                }
                catch (Exception e)
                {
                    if (e is ExperiedException)
                        expiredUsers.Add(users[i]);
                    else
                        //include AnalizingException UnknowLoginException and other real unknown error.
                        Program.WriteLog(e.Message, users[i]);
                }
                result += tempResult;
                Console.Write(tempResult);
            }

            ChangePassword();

            result += InspectChangedUser(waitForConfirmed);
            result = AnalizeCommaTextToHtml(result);

            if (confirmedUsers.Count > 0)
            {
                WriteNewBudNetUserFile();
                var support = new SupportRequest(confirmedUsers);
                try
                {
                    support.LogOnAndAdd();
                }
                catch (Exception e)
                {
                    WriteLog(e.Message);
                }

            }
            WriteTodayFile(result);
            Console.WriteLine("Input Any Key to Exit");
            Console.ReadKey();
        }
        public static void WriteTodayFile(string readyToWrite)
        {
            bool hasError = false;
            string backup = @".\Result\" + DateTime.Today.ToShortDateString().Replace("/", "-").Replace("\\", "-") + ".html";
            try
            {
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
                using (StreamWriter sw = new StreamWriter(backup, false))
                {
                    sw.Write(readyToWrite);
                }
            }
            catch
            {
                hasError = true;
                Console.WriteLine("Write today copyback file failed.Please check in");
            }
            if (!hasError)
            {
                using (StreamWriter sw = new StreamWriter(".\\Result\\today.html", false))
                {
                    sw.Write(readyToWrite);
                }

            }


        }
        public static void ReadFileToUsers(List<UserInfo> toUsers)
        {
            var sr = new StreamReader("bud.txt");
            while (!sr.EndOfStream)
            {
                var user = new UserInfo();
                user.customerID = sr.ReadLine().Trim();
                user.company = sr.ReadLine();
                user.username = sr.ReadLine().Trim();
                user.password = sr.ReadLine().Trim();
                user.locations = new List<location>();
                while (true)
                {
                    var location = new location();
                    location.name = sr.ReadLine();
                    if (location.name == "" || location.name == null)
                        break;
                    location.number = sr.ReadLine();
                    user.locations.Add(location);
                }
                toUsers.Add(user);
            }
            sr.Close();
        }
        public static string InspectChangedUser(List<UserInfo> waitToConfirm)
        {
            BudNetRequest budnetRequest;
            string result = "";
            for (var i = 0; i < waitToConfirm.Count; i++)
            {
                var tempResult = "";
                budnetRequest = new BudNetRequest(waitToConfirm[i]);
                try
                {
                    tempResult = budnetRequest.RequestJson();
                    for (int j = 0; j < users.Count; j++)
                    {
                        if (waitToConfirm[i].username == users[j].username)
                        {
                            users[j] = waitToConfirm[i];
                            confirmedUsers.Add(users[j]);
                        }
                    }
                }
                catch (ExperiedException)
                {
                    Program.WriteLog(" changed password failed.", waitToConfirm[i]);
                }
                catch (Exception e)
                {
                    Program.WriteLog(e.Message, users[i]);
                }

                result += tempResult;
                Console.Write(tempResult);
            }
            return result;
        }
        public static void ChangePassword()//it will auto making new password,write log.txt,create new bud.txt and write detail to CustomerNotes in support.
        {
            var newPassword = "";
            var isSucceed = false;
            foreach (var user in expiredUsers)
            {
                var budnetReuqestForPW = new BudNetRequest(user);
                newPassword = ChangePasswordPolicy(user.password);
                isSucceed = budnetReuqestForPW.RequestToChangePassword(user, newPassword);
            }

        }
        public static string ChangePasswordPolicy(string oldPassword)
        {
            string newValue = oldPassword;
            while (newValue == oldPassword)
                if (oldPassword == "pqrst2")
                    newValue = "pqrst1";
                else if (oldPassword == "pqrst1")
                    newValue = "pqrst2";
                else if (oldPassword == "BK8173")
                    newValue = "BK1973";
                else if (oldPassword == "BK1973")
                    newValue = "BK8173";
                else
                    newValue = oldPassword.Substring(0, 3) + new Random().Next(100, 9999);
            return newValue;
        }
        public static void WriteNewBudNetUserFile()
        {
            Console.WriteLine("Ready to write new bud.txt");
            Console.WriteLine("");
            var tempFilename = "tempNewUsers.txt";
            var sw = new StreamWriter(File.Create(tempFilename));
            foreach (var item in users)
            {
                sw.WriteLine(item.customerID);
                sw.WriteLine(item.company);
                sw.WriteLine(item.username);
                sw.WriteLine(item.password);
                foreach (var location in item.locations)
                {
                    sw.WriteLine(location.name);
                    sw.WriteLine(location.number);
                }
                sw.WriteLine("");
            }
            sw.Close();
            var budnetBackupFileName = "BudnetBackup" + DateTime.Now.Year + "." + DateTime.Now.Month + "." + DateTime.Now.Day + ".txt";
            if (File.Exists(budnetBackupFileName))
                File.Delete(budnetBackupFileName);
            File.Move("bud.txt", budnetBackupFileName);
            File.Move(tempFilename, "bud.txt");
            Console.WriteLine("New bud.txt has written.");
            Console.WriteLine("");
        }
        public static string AnalizeCommaTextToHtml(string source)
        {
            var sr = new StringReader(source);
            var result = new StringBuilder();
            string singleRecord = "";
            result.AppendLine("<html><body><div><table>");
            while (!(sr.Peek() == -1))
            {
                singleRecord = sr.ReadLine();
                if (singleRecord == "")
                    result.AppendLine("<tr><td>&nbsp;</td></tr>");

                else
                {
                    var tempArray = singleRecord.Split('^');
                    if (tempArray[0] == "Company")
                        result.AppendLine("<tr><td colspan=2>" + tempArray[1] + "</td></tr>");
                    else if (tempArray[0] == "Location")
                    {
                        var tempArray2 = tempArray[1].Split('%');
                        result.AppendLine("<tr><td colspan=2>Location:" + tempArray2[0] + "&nbps;&nbps;" + tempArray2[1] + "</td></tr> ");

                    }
                    else
                    {
                        count++;
                        result.AppendLine("<tr><td>" + tempArray[0] + "</td>");
                        if (tempArray[1] == "green")
                            result.AppendLine("<td><image src='green.gif'/></td></tr>");
                        else if (tempArray[1] == "yellow")
                            result.AppendLine("<td><image src='yellow.gif'/></td></tr>");
                        else if (tempArray[1] == "red")
                            result.AppendLine("<td><image src='red.gif'/></td></tr>");
                    }
                }

            }
            result.AppendLine("<tr></tr>");
            result.AppendLine("<tr><td>Count</td><td>" + count / 5 + "</td></tr>");
            result.AppendLine("</table></div>");
            result.AppendLine("</body></html>");
            return result.ToString();
        }
        public static void WriteLog(string logMessage, UserInfo user)
        {
            var sw = new StreamWriter("log.txt", true);
            var log = DateTime.Today.ToShortDateString() + "\r\n";
            log += user.company + "  " + logMessage + "\r\n";
            log += "CustomerID:   \t" + user.customerID + "\r\n";
            log += "username:      \t" + user.username + "\r\n";
            log += "oldpassword: \t" + user.password + "\r\n";
            log += "\r\n";
            sw.Write(log);
            sw.Close();
        }
        public static void WriteLog(string logMessage)
        {
            var sw = new StreamWriter("log.txt", true);
            var log = DateTime.Today.ToShortDateString() + "\r\n";
            log += logMessage + "\r\n";
            log += "\r\n";
            sw.Write(log);
            sw.Close();
        }
        public static string getInputValueByName(string name, string html)
        {
            string viewstate = "";//get viewstate
            int startIndex = html.IndexOf("name=\"" + name + "\"");
            int endIndex;
            viewstate = html.Substring(startIndex, 600);
            viewstate = viewstate.Substring(viewstate.IndexOf("value=\"") + 7);
            endIndex = viewstate.IndexOf("/>");
            viewstate = viewstate.Substring(0, endIndex);
            endIndex = viewstate.IndexOf("\"");
            viewstate = viewstate.Substring(0, endIndex);
            return viewstate;
        }
    }
    public class SupportRequest
    {
        public static List<UserInfo> waittingForAdd = new List<UserInfo>();
        public HttpWebRequest request;
        private string supportID;
        private string supportPW;//password
        private string Version;
        public SupportRequest(List<UserInfo> waitToAdd)
            : this("wuji", "mawjhao")
        {
            waittingForAdd = waitToAdd;
            Version = DateTime.Now.Year.ToString().Substring(2, 2) + "." + DateTime.Now.Month.ToString("00");
        }
        public SupportRequest(string id, string pw)
        {
            this.supportID = id;
            this.supportPW = pw;
        }
        public CookieCollection LoginSupport()
        {
            var LoginSucceed = false;
            while (!LoginSucceed)
            {
                request = (HttpWebRequest)WebRequest.Create("http://support.encompass8.com/ECP_" + Version + "/aspx1/Home.aspx");
                request.Method = "post";
                request.Accept = "text/html";
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.124 Safari/537.36";
                request.KeepAlive = false;
                request.ContentType = "application/x-www-form-urlencoded";
                request.CookieContainer = new CookieContainer();
                var acsii = new ASCIIEncoding();
                var data = acsii.GetBytes("LogOnType=LogOn&HTMLHeader=" + supportID + "&HTMLFooter=" + supportPW);
                request.ContentLength = data.Length;
                var writer = request.GetRequestStream();

                writer.Write(data, 0, data.Length);

                var response = (HttpWebResponse)request.GetResponse();
                var streamReader = new StreamReader(response.GetResponseStream());
                var html = streamReader.ReadToEnd();
                if (html.Contains("WuJi @ Support"))
                    LoginSucceed = true;
                else
                {
                    var lastVersion = Version;
                    LoginSucceed = false;
                    Version = DateTime.Now.Year.ToString().Substring(2, 2) + "." + ((int)DateTime.Now.Month + 1).ToString("00");
                    if (lastVersion == Version)
                    {
                        throw new SupportLoginInException();
                    }
                }

                response.Close();
            }

            return request.CookieContainer.GetCookies(new Uri("http://support.encompass8.com"));
        }
        public void AddCustomerNotes(string data, CookieCollection cookie, string customerID)
        {
            request = (HttpWebRequest)WebRequest.Create("http://support.encompass8.com/ECP_" + Version + "/aspx1/TableEditSubmit.aspx?Action=Add&TableEdit=CustomerNotes");
            request.Method = "post";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.124 Safari/537.36";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookie);
            request.ContentType = "application/x-www-form-urlencoded";
            var asc = new ASCIIEncoding();
            var bytes = asc.GetBytes(data);
            request.ContentLength = bytes.Length;
            var writer = request.GetRequestStream();
            writer.Write(bytes, 0, bytes.Length);
            var response = (HttpWebResponse)request.GetResponse();
            response.Close();
        }
        public void LogOnAndAdd()
        {
            var cookies = LoginSupport();
            var hasError = false;
            foreach (var item in waittingForAdd)
            {
                var customerNotes = "BudNet:%0D%0A";
                customerNotes += item.company + "%0D%0A";
                customerNotes += "budnet ID: " + item.username + "%0D%0A";
                customerNotes += "password: " + item.password + "%0D%0A";
                var postData = "CustomerID=" + item.customerID + "&CustomerNote=" + customerNotes;
                try
                {
                    AddCustomerNotes(postData, cookies, item.customerID);
                }
                catch
                {
                    hasError = true;
                }
                if (request.Address.ToString().ToLower().Contains("tablerelationships.aspx") && !hasError)
                    Program.WriteLog(" add customer note sucessfully.", item);
                else
                {
                    Program.WriteLog(" adds customer note failed.", item);
                }

            }


        }
    }
    //add a class like BudNetRequest to access Support to add detail in CustomerNotes for password  changed.
    public class BudNetRequest
    {
        public HttpWebRequest request;
        private UserInfo user;
        public BudNetRequest(UserInfo user)
        {
            this.user = user;
        }

        public byte[] MakeSendData(UserInfo user)
        {
            string username = user.username;
            string password = user.password;
            string realm = "BudNet-Prod";
            string postData = "username=" + username + "&password=" + password + "&realm=" + realm;
            ASCIIEncoding ascii = new ASCIIEncoding();
            byte[] data = ascii.GetBytes(postData);
            return data;
        }
        public bool RequestToChangePassword(UserInfo user, string newPassword)
        {
            byte[] data = MakeSendData(user);
            SetPost(data);
            var respone = request.GetResponse();
            if (respone.ResponseUri.ToString().ToLower().Contains("sso=p+login.aspx"))
            {
                var sr = new StreamReader(respone.GetResponseStream());
                var html = sr.ReadToEnd();
                sr.Close();

                string viewstate = Program.getInputValueByName("__VIEWSTATE", html);
                string eventvalidation = Program.getInputValueByName("__EVENTVALIDATION", html);
                viewstate = System.Web.HttpUtility.UrlEncode(viewstate);
                eventvalidation = System.Web.HttpUtility.UrlEncode(eventvalidation);

                if (html.Contains("Your password has expired!") || html.Contains("Your password expires in 2 days!") || html.Contains("Your password expires in 3 days!"))
                {
                    Program.WriteLog("'s password has expired and will be set a new as '" + newPassword + "'.", user);

                    var cookie = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
                    string sendData = "__EVENTTARGET=&__EVENTARGUMENT=&__VIEWSTATE=" + viewstate + "&__EVENTVALIDATION=" + eventvalidation + "&txtCurrentPassword=" + user.password + "&txtNewPassword=" + newPassword + "&txtConfirmNewPassword=" + newPassword + "&cmdSubmit=Submit";
                    var ascii = new ASCIIEncoding();
                    data = ascii.GetBytes(sendData);
                    try
                    {
                        SetPost(data, "https://www.budnet.com/DSLogin/,DanaInfo=.a184C65F999JAF,SSO=P+ChangePassword.aspx", cookie);
                        var response = (HttpWebResponse)request.GetResponse();
                        respone.Close();
                        request = null;
                        var newUser = user;
                        newUser.password = newPassword;
                        Program.waitForConfirmed.Add(newUser);
                        return true;//true or false is meaningless
                    }
                    catch
                    {
                        Program.WriteLog(" sending new password has some errors,but not sure what it's. ", user);
                    }
                    return false;
                }
                else
                    Program.WriteLog(" should be expired,but re-inspect wrongly.", user);

            }
            else
            {
                Program.WriteLog(" should be expired,but re-inspect wrongly.", user);
            }
            respone.Close();
            return false;




        }
        public string RequestJson()
        {
            byte[] data = MakeSendData(user);
            SetPost(data);
            var tempResponse = (HttpWebResponse)request.GetResponse();
            var tempsr = new StreamReader(tempResponse.GetResponseStream());
            var html = tempsr.ReadToEnd();
            tempsr.Close();

            tempResponse.Close();
            #region error finding
            if (request.Address.ToString().ToLower().Contains("welcome.cgi?p=failed".ToLower()))
            {
                request = null;
                throw (new UnkonwnLoginException());
            }
            else if (request.Address.ToString().ToLower().Contains("welcome.cgi?p=passwordExpiration".ToLower()))
            {
                var cookie = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
                request.Abort();
                //respone.Close();
                request = null;
                request = (HttpWebRequest)WebRequest.Create(new Uri("https://www.budnet.com/dana/home/starter0.cgi"));
                request.Method = "get";
                request.KeepAlive = false;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.81 Safari/537.36";
                request.Accept = "text/html";
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.Add(cookie);
                var response = (HttpWebResponse)request.GetResponse();
                response.Close();
            }
            else if (request.Address.ToString().ToLower().EndsWith("DSLogin/,DanaInfo=.a184C65F999JAF,SSO=P+Login.aspx".ToLower()))
            {
                if (html.Contains("Your password has expired!") || html.Contains("Your password expires in 2 days!") || html.Contains("Your password expires in 3 days!"))
                {
                    throw (new ExperiedException());
                }
            }
            #endregion
            var cookies = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
            request = null;
            string jsonData;
            var htmlResult = new StringBuilder();
            htmlResult.AppendLine("Company^" + user.company);
            if (user.locations.Count > 1)
            {
                foreach (var location in user.locations)
                {
                    htmlResult.AppendLine("Location^" + location.name + "%" + location.number);
                    SetGetJson(cookies, location.number);
                    var responsed = (HttpWebResponse)request.GetResponse();
                    cookies = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
                    var sr = new StreamReader(responsed.GetResponseStream());
                    jsonData = sr.ReadToEnd().Replace("__type", "type");
                    responsed.Close();
                    htmlResult.AppendLine(AnalizeJson(jsonData));
                }
            }
            else
            {
                SetGetJson(cookies, "");
                var responsed = (HttpWebResponse)request.GetResponse();
                var sr = new StreamReader(responsed.GetResponseStream());
                jsonData = sr.ReadToEnd().Replace("__type", "type");
                responsed.Close();
                htmlResult.AppendLine(AnalizeJson(jsonData));
            }
            htmlResult.AppendLine("");
            request = null;
            return htmlResult.ToString();
        }
        private string AnalizeJson(string jsonData)
        {
            System.Web.Script.Serialization.JavaScriptSerializer jss = new System.Web.Script.Serialization.JavaScriptSerializer();
            Root r;
            try
            {
                r = jss.Deserialize<Root>(jsonData);
            }
            catch
            {
                throw (new AnalizingException());
            }

            var result = new StringBuilder();
            var htmlResult = new StringBuilder();
            var currenUserColor = "green";
            foreach (var item in r.d.Groups)
            {
                var currentColor = "green";

                foreach (var singleCircle in item.Metrics)
                {
                    if (singleCircle.ImageCssClass == "yellow" && currentColor != "red")
                    {
                        currentColor = "yellow";
                        if (currenUserColor != "red")
                            currenUserColor = "yellow";
                    }
                    else if (singleCircle.ImageCssClass == "red")
                    {
                        currenUserColor = "red";
                        currentColor = "red";
                    }
                }
                result.AppendLine(item.name.Substring(0, 5) + "\t is\t  " + currentColor + "\t");
                htmlResult.AppendLine(item.name + "^" + currentColor);

            }
            result.AppendLine("Final \t is\t  " + currenUserColor + "\t");
            htmlResult.AppendLine("Final^" + currenUserColor);
            result.AppendLine("");
            return htmlResult.ToString();
        }
        public void SetPost(byte[] data, string url = "", CookieCollection cookie = null)
        {
            if (url == "")
                request = (HttpWebRequest)WebRequest.Create("https://www.budnet.com/dana-na/auth/url_15/login.cgi");
            else
                request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "post";
            request.KeepAlive = false;
            request.Headers.Add("Accept-Language", "en-US,en;");
            request.CookieContainer = new CookieContainer();
            if (cookie != null)
                request.CookieContainer.Add(cookie);
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.81 Safari/537.36";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/xhtml+xml,application/xml,";
            request.ContentLength = data.Length;
            var st = request.GetRequestStream();
            st.Write(data, 0, data.Length);
            st.Close();


        }
        private void SetGetJson(CookieCollection cookies, string location)
        {

            if (location != "")
            {
                request = (HttpWebRequest)WebRequest.Create(@"https://www.budnet.com/DQP/Services/Dashboard.svc/,DanaInfo=.a184C65F999JAF,dom=1,CT=sxml+GetMetrics?wholesalerId=" + location);
            }
            else
                request = (HttpWebRequest)WebRequest.Create(@"https://www.budnet.com/DQP/Services/Dashboard.svc/,DanaInfo=.a184C65F999JAF,dom=1,CT=sxml+GetMetrics?");


            request.Method = "get";
            request.KeepAlive = false;
            //request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.81 Safari/537.36";
            request.Accept = "application/json";
            request.Host = "www.budnet.com";
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);

        }

    }

    public struct UserInfo
    {
        public string customerID;
        public string company;
        public string username;
        public string password;
        public List<location> locations;

    }
    public struct location
    {
        public string number;
        public string name;
    }
    public class ExperiedException : Exception
    {
        public ExperiedException()
            : base(" password is expired.")
        { }
    }
    public class AnalizingException : Exception
    {
        public AnalizingException()
            : base(" has errors in anazling process.")
        { }
    }
    public class UnkonwnLoginException : Exception
    {
        public UnkonwnLoginException()
            : base(" has some Unkonwn errors.")
        { }
    }
    public class SupportLoginInException : Exception
    {
        public SupportLoginInException()
            : base(" has some errors in loging Support.")
        {

        }
    }
}
