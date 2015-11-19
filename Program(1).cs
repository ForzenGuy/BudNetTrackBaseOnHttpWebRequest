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
        static void Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 50;
            expiredUsers = new List<UserInfo>();
            waitForConfirmed = new List<UserInfo>();
            confirmedUsers = new List<UserInfo>();
            users = new List<UserInfo>();
            #region production code
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
                if (user.locations.First<location>().number != null)
                    users.Add(user);
            }
            sr.Close();
            BudNetRequest budnetRequest;
            if (!Directory.Exists("\\Result"))
            {
                var dir = Directory.CreateDirectory(".\\Result");
                dir = null;
            }

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
                    if (e.Message == "analizing error")
                    { 
                    }
                    else
                        Program.WriteLog(e.Message, users[i]);
                }

                result += tempResult;
                Console.Write(tempResult);
            }
            result = AnalizeCommaTextToHtml(result);
            StreamWriter sw = new StreamWriter(".\\Result\\today.html", false);
            sw.Write(result);
            sw.Close();

            //ready to  check if there are passwords been changed and do next work.
            //
            //
            //ChangePassword();
            Console.WriteLine("Input Any Key to Exit");
            Console.ReadKey();
            #endregion
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
                if (isSucceed)
                {
                    foreach (var waitCUser in waitForConfirmed)
                    {
                        var budnetRequest = new BudNetRequest(waitCUser);
                        try
                        {
                            budnetRequest.RequestJson();
                            confirmedUsers.Add(waitCUser);
                        }
                        catch
                        {
                            WriteLog("change password failed.", waitCUser);
                        }
                    }

                }
            }
            var support = new SupportRequest(confirmedUsers);

        }
        public static string ChangePasswordPolicy(string oldPassword)
        {
            string newValue = oldPassword;
            while (newValue == oldPassword)
                if (oldPassword == "pqrst2")
                    newValue = "pqrst1";
                else if (oldPassword == "pqrst1")
                    newValue = "pqrst2";
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
            File.Move("bud.txt", "BudnetBackup" + DateTime.Now.Year + "." + DateTime.Now.Month + "." + DateTime.Now.Day + ".txt");
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
    }
    public class SupportRequest
    {
        public static List<UserInfo> waittingForAdd = new List<UserInfo>();
        public HttpWebRequest request;
        private string supportID;
        private string supportPW;//password
        public SupportRequest(List<UserInfo> waitToAdd)
            : this("wuji%24", "mawjhao")
        {
            waittingForAdd = waitToAdd;
        }
        public SupportRequest(string id, string pw)
        {
            this.supportID = id;
            this.supportPW = pw;
        }
        public CookieCollection LoginSupport()
        {
            request = (HttpWebRequest)WebRequest.Create("http://support.encompass8.com/ECP_15.07/aspx1/Default.aspx?");
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
            response.Close();
            return request.CookieContainer.GetCookies(new Uri("http://support.encompass8.com"));
        }
        public void AddCustomerNotes(string data, CookieCollection cookie, string customerID)
        {
            request = (HttpWebRequest)WebRequest.Create("http://support.encompass8.com/ECP_15.07/aspx1/TableEditSubmit.aspx?Action=Add&TableEdit=CustomerNotes&FieldName=CustomerID&FieldValue=" + customerID + "&CurValue=" + customerID + "&TableName=Customers&Search=%7cCustomerID%7e" + customerID + "%7eE%7c&SubTable=CustomerNotes&LinkField=CustomerID&SubLinkField=CustomerID&");
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
        public void LogOnAndAdd(string data, string customerID)
        {
            var cookies = LoginSupport();
            var hasError = false;
            foreach (var item in waittingForAdd)
            {
                var customerNotes = "BudNet:<br/>";
                customerNotes += "username:" + item.username + "<br/>";
                customerNotes += "password:" + item.password + "<br/>";
                try
                {
                    AddCustomerNotes(customerNotes, cookies, customerID);
                }
                catch
                {

                    hasError = true;
                }
                if (request.Address.ToString().ToLower().Contains("tableview.aspx") && !hasError)
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

                if (html.Contains("Your password has expired!"))
                {
                    Program.WriteLog("'s password has expired and will be set a new as ' " + newPassword + " '.", user);

                    var cookie = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
                    string sendData = "txtCurrentPassword=" + user.password + "&txtNewPassword=" + newPassword + "&txtConfirmNewPassword=" + newPassword + "&";
                    var ascii = new ASCIIEncoding();
                    data = ascii.GetBytes(sendData);
                    try
                    {
                        SetPost(data, "https://www.budnet.com/DSLogin/,DanaInfo=.a184C65F999JAF,SSO=P+ChangePassword.aspx", cookie);
                        var response = (HttpWebResponse)request.GetResponse();
                        respone.Close();
                        var newUser = user;
                        newUser.password = newPassword;
                        Program.waitForConfirmed.Add(newUser);
                        Program.WriteLog(" has sent  new password sucessfully.", user);
                        return true;//true or false is meaningless
                    }
                    catch
                    {
                        Program.WriteLog(" sending task has some errors,but not sure that it's ", user);
                    }
                    return false;
                }

            }

            Program.WriteLog("'s password has expired and can do nothing to it.", user);
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
                Program.WriteLog("has expired and been changed by someone.", user);
                Console.WriteLine(user.company + "  has some unkonwn error.\r\n");
                request = null;
                //respone.Close();
                return "";
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
                if (html.Contains("Your password has expired!"))
                {
                    Program.WriteLog("'s password is expired.", user);
                    Program.expiredUsers.Add(user);
                    return "";
                }              
            }

            //respone.Close();//important!!
            request.Abort();
            #endregion

            var cookies = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));

            string jsonData;
            var resultText = new StringBuilder();
            var htmlResult = new StringBuilder();
            //resultText.AppendLine(user.company);
            htmlResult.AppendLine("Company^" + user.company);
            if (user.locations.Count > 1)
            {
                foreach (var location in user.locations)
                {
                    //resultText.AppendLine("Location:\t" + location.name + "\t" + location.number);
                    htmlResult.AppendLine("Location^" + location.name + "%" + location.number);
                    SetGetJson(cookies, location.number);
                    var responsed = (HttpWebResponse)request.GetResponse();
                    cookies = request.CookieContainer.GetCookies(new Uri("https://www.budnet.com/"));
                    var sr = new StreamReader(responsed.GetResponseStream());
                    jsonData = sr.ReadToEnd().Replace("__type", "type");
                    responsed.Close();
                    //resultText.AppendLine(AnalizeJson(jsonData));
                    try
                    {
                        htmlResult.AppendLine(AnalizeJson(jsonData));
                    }
                    catch
                    {
                        htmlResult.AppendLine("can't analizing json data.^");
                        Program.WriteLog("has error in analizing jsondata.", user);
                        throw (new Exception("analizing error"));
                    }

                }
            }
            else
            {
                SetGetJson(cookies, "");
                var responsed = (HttpWebResponse)request.GetResponse();
                var sr = new StreamReader(responsed.GetResponseStream());
                jsonData = sr.ReadToEnd().Replace("__type", "type");
                responsed.Close();
                //resultText.AppendLine(AnalizeJson(jsonData));
                try
                {
                    htmlResult.AppendLine(AnalizeJson(jsonData));
                }
                catch
                {
                    htmlResult.AppendLine("can't analizing json data.^");
                    Program.WriteLog("has error in analizing jsondata.", user);
                    throw (new Exception("analizing error"));
                }

            }
            resultText.AppendLine("");
            htmlResult.AppendLine("");
            request = null;
            return htmlResult.ToString();
        }
        private string AnalizeJson(string jsonData)
        {
            System.Web.Script.Serialization.JavaScriptSerializer jss = new System.Web.Script.Serialization.JavaScriptSerializer();
            Root r = jss.Deserialize<Root>(jsonData);
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

        public bool ChangePassoword(UserInfo expiredUser)
        {

            return true;
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
}
