using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodoApi.Models;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System;
using SixLabors.ImageSharp;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController : ControllerBase
    {
        private readonly TodoContext _context;

        public TodoController(TodoContext context)
        {
            _context = context;

            if (_context.TodoItems.Count() == 0)
            {
                // Create a new TodoItem if collection is empty,
                // which means you can't delete all TodoItems.
                _context.TodoItems.Add(new TodoItem { Name = "Item1" });
                _context.SaveChanges();
            }
        }

        // GET: api/Todo
        [HttpGet]
        // public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems()
        // {
        //     return await _context.TodoItems.ToListAsync();
        // }

        // GET: api/Todo/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoItem>> GetTodoItem(long id)
        {
            var todoItem = await _context.TodoItems.FindAsync(id);

            if (todoItem == null)
            {
                return NotFound();
            }

            return todoItem;
        }

        // POST: api/Todo
        [HttpPost]
        public string GetValue([FromBody] string content)
        {
            //生成语音
            string testStr = content.Replace("\n", "").Replace(" ","").Replace("\t","").Replace("\r","");

            genVoice(testStr);
            //生成素材图片
            ArrayList fileList =  MyList();
            //生成文件总数量
            int j = 0; 
            foreach (string i in fileList)
            {
                j += 1;
                genPic(i,"img" + j.ToString());
            }
            //合成视频
            genVideo(j,calVoice());
            //生成字幕文件
            double subLong = testStr.Length;//字符串总长度
            double perSub = subLong/calVoice();//每一秒多少字
            //genText("aaa.srt");
            double perSec = 0; //每一句字幕持续多少秒
            string subContent = ""; //字幕内容
            decimal cirTime = Math.Ceiling((decimal)subLong/(decimal)25); //循环的次数
            string test = "";
            TimeSpan time,time1;
            perSec = 25 / perSub;
            for(int i=1;i<cirTime;i++)
            {
                test += i.ToString();
                time = TimeSpan.FromSeconds(perSec * (1-i));
                time1 = TimeSpan.FromSeconds(perSec * i);
                subContent += i.ToString() + "\r\n" + time .ToString(@"hh\:mm\:ss\,fff") + " --> " + time1 .ToString(@"hh\:mm\:ss\,fff") + "\r\n" + testStr.Substring((i - 1) * 25, 25) +"\r\n\r\n";
                
            }
            genText(subContent);
            return subLong.ToString() + "///" + perSub.ToString() + "///" + perSec.ToString() + "///";
        }
        
        //读取MP3文件时长
        static int calVoice()
        {
            ProcessStartInfo start = new ProcessStartInfo("afinfo");//设置运行的命令行文件问ping.exe文件，这个文件系统会自己找到
            //如果是其它exe文件，则有可能需要指定详细路径，如运行winRar.exe
            start.Arguments = " tts.mp3 | grep \"estimated duration\"";//设置命令参数
            start.CreateNoWindow = false;//不显示dos命令行窗口
            start.RedirectStandardOutput = true;//
            start.RedirectStandardInput = true;//
            start.UseShellExecute = false;//是否指定操作系统外壳进程启动程序
            Process p=Process.Start(start);
            StreamReader reader = p.StandardOutput;//截取输出流
            string line = reader.ReadLine();//每次读取一行
            while (!reader.EndOfStream)
            {   
                line = reader.ReadLine();
                if (line.Contains("duration"))
                {
                    break;
                }
            }
            p.WaitForExit();//等待程序执行完退出进程
            p.Close();//关闭进程
            reader.Close();//关闭流
            string result = new Regex(@"-?[1-9]\d*").Match(line).Value;
            return int.Parse(result);
        }

        //合成语音
        static int genVoice(string text)
        {
            var API_KEY = "cngu1wvCy4edGGoXVjPaoSOG";
            var SECRET_KEY = "bGI7m7r0a5k2cAP9DM90yGXHc4BKqaTN";

            var client = new Baidu.Aip.Speech.Tts(API_KEY, SECRET_KEY);
            client.Timeout = 60000;  // 修改超时时间

            var option = new Dictionary<string, object>()
            {
                {"spd", 5}, // 语速
                {"vol", 5}, // 音量
                {"per", 1}  // 发音人，4：情感度丫丫童声
            };

            //超过百度API512字限制的情况
            if (text.Length > 511)
            {
                int errCode = 0;
                decimal cirTime = Math.Ceiling((decimal)text.Length/(decimal)511); //循环的次数

                for (int i = 1; i < cirTime; i ++)
                {
                    var result = client.Synthesis(text.Substring((i - 1) * 511, 511), option);

                    if (result.ErrorCode == 0)  // 或 result.Success
                    {
                        System.IO.File.WriteAllBytes("tts" + i + ".mp3", result.Data);
                    }

                    errCode += result.ErrorCode;
                }

                var result1 = client.Synthesis(text.Substring(((int)cirTime - 1) * 511, text.Length - ((int)cirTime - 1) * 511), option);

                    if (result1.ErrorCode == 0)  // 或 result.Success
                    {
                        System.IO.File.WriteAllBytes("tts" + cirTime + ".mp3", result1.Data);
                    }

                    errCode += result1.ErrorCode;
                
                //合并MP3参数;
                string argStr = "-y ";
                for (int i=0; i<cirTime; i++)
                {
                    argStr += "-i tts"+ (i+1).ToString() +".mp3  ";
                }
                argStr += "-filter_complex \"";
                for (int i=0; i<cirTime; i++)
                {
                    argStr += "[" +i+ ":0] ";
                }
                argStr += "concat=n=" +cirTime.ToString();
                argStr += ":v=0:a=1 [a]\" -map [a] tts.mp3";

                ProcessStartInfo start = new ProcessStartInfo("ffmpeg");//设置运行的命令行文件问ping.exe文件，这个文件系统会自己找到
                //如果是其它exe文件，则有可能需要指定详细路径，如运行winRar.exe
                start.Arguments = argStr;//设置命令参数
                start.CreateNoWindow = false;//不显示dos命令行窗口
                start.RedirectStandardOutput = true;//
                start.RedirectStandardInput = true;//
                start.UseShellExecute = false;//是否指定操作系统外壳进程启动程序
                Process p=Process.Start(start);
                p.WaitForExit();//等待程序执行完退出进程
                p.Close();//关闭进程

                return errCode;
                
            }
            else
            {
                var result = client.Synthesis(text, option);

                if (result.ErrorCode == 0)  // 或 result.Success
                {
                    System.IO.File.WriteAllBytes("tts.mp3", result.Data);
                }
                return result.ErrorCode;
            }

            

            
        }

        //合成图片
        static void genPic(string file, string outFile)
        {
            var image1 = Image.Load(file);
            image1.Mutate
            (x => x
                .Resize(1280, 720)
            );
            using (var img = Image.Load("/Users/harold/Downloads/video/TodoApi/back.png"))
            {
                img.Mutate(i => i.DrawPolygon(Rgba32.DarkGray, 5, new SixLabors.Primitives.Point(315, 125),new SixLabors.Primitives.Point(1600, 125),new SixLabors.Primitives.Point(1600, 850),new SixLabors.Primitives.Point(315, 850)));
                img.Mutate(i => i.DrawImage(image1, 1, new SixLabors.Primitives.Point(318, 128)));
                img.Save(outFile + ".jpeg");
            }
        }

        //合成字幕
        static void genText(string content)
        {
    　　    try
        　　　　{
        　　　　　　using (FileStream fs = new FileStream("sub.srt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write))
        　　　　　　{
        　　　　　　　　StreamWriter sw = new StreamWriter(fs);
        　　　　　　　　sw.Write(content);
        　　　　　　　　sw.Flush();
        　　　　　　　　sw.Close();
        　　　　　　　　fs.Close();
        　　　　　　}
        　　　　}
            catch
        　　　　{
        　　　　　　//"保存文本文件出错！"
        　　　　}
        }

        // 返回文件夹下所有图片
        public ArrayList MyList()
        {
            
            ArrayList listNew = new ArrayList();

            string[]   filenames=Directory.GetFiles("/Users/harold/Downloads/video/TodoApi/pic/"); 
            foreach (string   files   in   filenames) 
            { 
                if (!files.Contains("DS_Store")) 
                {
                    listNew.Add(files);
                }
            }

            return listNew;
            
        }

        //合成视频
        static void genVideo(int picNum, int vocLen)
        {
            string fps = ((float)picNum/vocLen).ToString();
            ProcessStartInfo start = new ProcessStartInfo("ffmpeg");//设置运行的命令行文件问ping.exe文件，这个文件系统会自己找到
            //如果是其它exe文件，则有可能需要指定详细路径，如运行winRar.exe
            start.Arguments = "-y -r "+ fps +" -i img%d.jpeg -stream_loop 0 -i tts.mp3 -vcodec mpeg4 -s 1920*1080 res.mp4";//设置命令参数
            start.CreateNoWindow = false;//不显示dos命令行窗口
            start.RedirectStandardOutput = true;//
            start.RedirectStandardInput = true;//
            start.UseShellExecute = false;//是否指定操作系统外壳进程启动程序
            Process p=Process.Start(start);
            StreamReader reader = p.StandardOutput;//截取输出流
            string line = reader.ReadLine();//每次读取一行
            while (!reader.EndOfStream)
            {   
                line += reader.ReadLine();
            }
            p.WaitForExit();//等待程序执行完退出进程
            p.Close();//关闭进程
            reader.Close();//关闭流
            
        }
        // 分词
        // API_KEY = "v19386WiGR9i01P4QW169EZg";
        // SECRET_KEY = "Gfyz8U3SWbDYfAArElsW8VuNC2xQGIfW";

        // var client_key = new Baidu.Aip.Nlp.Nlp(API_KEY, SECRET_KEY);
        // client_key.Timeout = 60000;  // 修改超时时间

        // var data = Encoding.GetEncoding("GBK").GetBytes(testStr);

        // var result_key = client_key.Lexer(data);
    }

    
}