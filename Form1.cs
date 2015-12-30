using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;


namespace PC_Terminal
{
    public partial class Form1 : Form
    {
        String StringReceive = "";
        byte[] receive_buff = new byte[9600];
        Color[] colors = new Color[255];
        Int32 gen_median = 0;
        UInt32[,] lepton_image = new UInt32[80, 80];
        UInt32[,] draw_lepton_image = new UInt32[80, 80];
        static int MAX_IMAGE_SIZE = (80 * 60 * 2);
        byte[] image_buffer = new byte[MAX_IMAGE_SIZE];
        int image_buffer_index = 0;
        int parser_state = 0;
        int parser_i;
        int parser_j;
        char[] buf = new char[10240];
        Random rand = new Random();
        TcpClient tcpClient = new TcpClient();
        NetworkStream netstream;

        public Form1()
        {
            InitializeComponent(); // Brandon is my master
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            int i;
            for (i = 0; i < 255; i++)
            {
                //black to blue
                colors[i] = Color.FromArgb(i, i, i);
                // blue to cyan (0,0,255)
                //colors[i + 256] = Color.FromArgb(0, i, 255);
                // Cyan to green (0,255,255)
                //colors[i + 512] = Color.FromArgb(0, 255, 255 - i);
                // Green to yellow (0,255,0)
                //colors[i + 768] = Color.FromArgb(i, 255, 0);
                // Yellow (255,255,0) to Red (255,0,0)
                //colors[i + 1024] = Color.FromArgb(255, 255 - i, 0);
            }
            colors[0] = Color.FromArgb(0, 0, 0); //black

            listBox1.SelectedIndex = 0;
        }
        public Int32 init()
        {
            if (listBox1.SelectedIndex == 0)
            {
                return 0;
            }
            else if (listBox1.SelectedIndex == 1 || listBox1.SelectedIndex == 2)
            {
                return 0;
            }
            throw new Exception("y");
        }
        
        
        // MOST IMPORTANT SHIT HERE START
        private void BtnOnOff_Click(object sender, EventArgs e)
        {
            String A = StringReceive;
            StringReceive = "";

            if (BtnOnOff.Text == "Connect")
            {
                try
                {
                    //ip = richTextBox1.Text;
                    //port = Convert.ToInt32(richTextBox2.Text);
                    //tcpClient.Connect(ip, port);

                    var proc = Task.Factory.StartNew(() =>
                    {
                        tcpClient.Connect("192.168.10.9", 26);
                        // 9606 is not a mistake, we have 6 garbage
                        // bytes from the rss232->ethernet adapter.
                        tcpClient.ReceiveBufferSize = 9606;
                        tcpClient.NoDelay = false;
                        netstream = tcpClient.GetStream();
                        reading();
                    });

                    BtnOnOff.Text = "Disconnect";
                    //ts1.Text = "Connected";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.StackTrace, "lalala", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ts1.Text = "Error ";
                }
            }
            else
            {
                // tcpClient.Close();
                BtnOnOff.Text = "Connect";
                ts1.Text = "Disconnected";
                tcpClient.Close();

            }
        }
        // MOST IMPORTANT SHIT HERE END
        

        public void scale_image()
        {
            int i;
            int j;
            UInt32 max = 0;
            UInt32 min = 0xffff;

            for (i = 0; i < 60; i++)
            {
                for (j = 0; j < 80; j++)
                {
                    if (draw_lepton_image[i, j] > max)
                    {
                        max = draw_lepton_image[i, j];
                    }

                    if ((draw_lepton_image[i, j] < min) && (draw_lepton_image[i, j] != 0))
                    {
                        min = draw_lepton_image[i, j];
                    }
                }
            }

            for (i = 0; i < 60; i++)
            {
                for (j = 0; j < 80; j++)
                {
                    draw_lepton_image[i, j] = draw_lepton_image[i, j] - min;
                    // Required noramlization
                    draw_lepton_image[i, j] = ((draw_lepton_image[i, j] * 255) / (max - min)) + 1;
                }
            }
        }
        public void copy_image()
        {
            int i;
            int j;

            for (i = 0; i < 60; i++)
            {
                for (j = 0; j < 80; j++)
                {
                    draw_lepton_image[i, j] = lepton_image[i, j];
                }
            }
        }
        public void reading()
        {
            // It's 4 because that gives out the best result
            // --> figured out by manually testing values
            var arbitary_readSize = 4;
            var rec_buff = new byte[tcpClient.ReceiveBufferSize];

            while (true)
            {
                try
                {
                    if (netstream.CanRead)
                    {
                        netstream.Read(rec_buff, 0, arbitary_readSize);
                        for (var receive_buff_cnt = 0;
                            receive_buff_cnt < arbitary_readSize;
                            receive_buff_cnt++)
                        {
                            var input = rec_buff[receive_buff_cnt];
                            parse_binary(input);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("Exception: {0}", e);
                    Invoke((Action)(() =>
                    {
                        ts1.Text = "Error: Cannot close this comm";
                        // XXX this shows up twice every two seconds,
                        // and is quite annying, therefore commented out.
                        //MessageBox.Show(this, "Cannot close this comm.\nIs possible to be unpluged usb adaptor", "Open comm error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }
        }
        public void parse_binary(byte input)
        {
            switch (parser_state)
            {
                case 0:
                    if (input == 0xde)
                    {
                        //    ts1.Text = ("got header");
                        parser_state = 1;
                    }
                    else
                    {
                        //   ts1.Text = (".");
                    }
                    break;
                case 1:
                    if (input == 0xad)
                    {
                        parser_state = 2;
                    }
                    else if (input == 0xde)
                    {
                        parser_state = 1;
                    }
                    else
                    {
                        //  ts1.Text = "error"+input.ToString();
                        parser_state = 0;
                    }
                    break;
                case 2:
                    if (input == 0xbe)
                    {
                        parser_state = 3;
                    }
                    else
                    {
                        // ts1.Text = "error" + input.ToString();
                        parser_state = 0;
                    }
                    break;
                case 3:
                    if (input == 0xef)
                    {
                        //ts1.Text = ("got header sync\n");
                        parser_state = 4;
                        image_buffer_index = 0;
                        parser_i = 0;
                        parser_j = 0;
                    }
                    else
                    {
                        parser_state = 0;
                    }
                    break;
                case 4:

                    if ((image_buffer_index <= MAX_IMAGE_SIZE))
                    {
                        if (image_buffer_index % 2 == 0)
                        {
                            // A conditional that we are trying to get rid off.
                            if (input > 50)
                                break;
                            else 
                                lepton_image[parser_i, parser_j] = (UInt32)input << 8;
                        }
                        else
                        {
                            lepton_image[parser_i, parser_j] |= (UInt32)input;
                            parser_j++;

                            if (parser_j >= 80)
                            {
                                parser_j = 0;
                                parser_i++;
                            }
                        }

                        image_buffer[image_buffer_index++] = (byte)input;

                        if (image_buffer_index == MAX_IMAGE_SIZE)
                        {
                            //ts1.Text = ("got all data\n");
                            //convert_bytes_to_image();
                            //print_image();
                            //save_pgm_file();

                            copy_image();
                            //norm_image();
                            scale_image();
                            writing();
                            parser_state = 0;
                            image_buffer_index = 0;
                        }
                    }
                    else
                    {
                        //    ts1.Text = ("error" + input.ToString());
                        parser_state = 0;
                    }
                    break;
            }
            //  if(parser_state != 0)
            //  {
            //      timeout_counter = 0;
            //  }
        }
        public void writing()
        {
            var img = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            int y, x;
            for (x = 0; x < 60; x++)
            {
                for (y = 0; y < 80; y++)
                {
                    try
                    {
                        int data;
                        Color newColor;
                        data = (int)(draw_lepton_image[x, y]);
                        newColor = colors[((data % 255))];
                        if (numericUpDown2.Value == 1)
                        {
                            img.SetPixel(y, x, newColor);
                        }
                        else if (numericUpDown2.Value == 2)
                        {
                            img.SetPixel((y * 2), (x * 2), newColor);
                            img.SetPixel((y * 2) + 1, (x * 2), newColor);
                            img.SetPixel((y * 2), (x * 2) + 1, newColor);
                            img.SetPixel((y * 2) + 1, (x * 2) + 1, newColor);
                        }
                        else if (numericUpDown2.Value == 3)
                        {
                            img.SetPixel((y * 3), (x * 3), newColor);
                            img.SetPixel((y * 3) + 1, (x * 3), newColor);
                            img.SetPixel((y * 3) + 2, (x * 3), newColor);
                            img.SetPixel((y * 3), (x * 3) + 1, newColor);
                            img.SetPixel((y * 3) + 1, (x * 3) + 1, newColor);
                            img.SetPixel((y * 3) + 2, (x * 3) + 1, newColor);
                            img.SetPixel((y * 3), (x * 3) + 2, newColor);
                            img.SetPixel((y * 3) + 1, (x * 3) + 2, newColor);
                            img.SetPixel((y * 3) + 2, (x * 3) + 2, newColor);
                        }
                        else if (numericUpDown2.Value == 4)
                        {
                            img.SetPixel((y * 4), (x * 4), newColor);
                            img.SetPixel((y * 4) + 1, (x * 4), newColor);
                            img.SetPixel((y * 4) + 2, (x * 4), newColor);
                            img.SetPixel((y * 4) + 3, (x * 4), newColor);
                            img.SetPixel((y * 4), (x * 4) + 1, newColor);
                            img.SetPixel((y * 4) + 1, (x * 4) + 1, newColor);
                            img.SetPixel((y * 4) + 2, (x * 4) + 1, newColor);
                            img.SetPixel((y * 4) + 3, (x * 4) + 1, newColor);
                            img.SetPixel((y * 4), (x * 4) + 2, newColor);
                            img.SetPixel((y * 4) + 1, (x * 4) + 2, newColor);
                            img.SetPixel((y * 4) + 2, (x * 4) + 2, newColor);
                            img.SetPixel((y * 4) + 3, (x * 4) + 2, newColor);
                            img.SetPixel((y * 4), (x * 4) + 3, newColor);
                            img.SetPixel((y * 4) + 1, (x * 4) + 3, newColor);
                            img.SetPixel((y * 4) + 2, (x * 4) + 3, newColor);
                            img.SetPixel((y * 4) + 3, (x * 4) + 3, newColor);
                        }
                        else if (numericUpDown2.Value == 5)
                        {
                            img.SetPixel((y * 5), (x * 5), newColor);
                            img.SetPixel((y * 5) + 1, (x * 5), newColor);
                            img.SetPixel((y * 5) + 2, (x * 5), newColor);
                            img.SetPixel((y * 5) + 3, (x * 5), newColor);
                            img.SetPixel((y * 5) + 4, (x * 5), newColor);
                            img.SetPixel((y * 5), (x * 5) + 1, newColor);
                            img.SetPixel((y * 5) + 1, (x * 5) + 1, newColor);
                            img.SetPixel((y * 5) + 2, (x * 5) + 1, newColor);
                            img.SetPixel((y * 5) + 3, (x * 5) + 1, newColor);
                            img.SetPixel((y * 5) + 4, (x * 5) + 1, newColor);
                            img.SetPixel((y * 5), (x * 5) + 2, newColor);
                            img.SetPixel((y * 5) + 1, (x * 5) + 2, newColor);
                            img.SetPixel((y * 5) + 2, (x * 5) + 2, newColor);
                            img.SetPixel((y * 5) + 3, (x * 5) + 2, newColor);
                            img.SetPixel((y * 5) + 4, (x * 5) + 2, newColor);
                            img.SetPixel((y * 5), (x * 5) + 3, newColor);
                            img.SetPixel((y * 5) + 1, (x * 5) + 3, newColor);
                            img.SetPixel((y * 5) + 2, (x * 5) + 3, newColor);
                            img.SetPixel((y * 5) + 3, (x * 5) + 3, newColor);
                            img.SetPixel((y * 5) + 4, (x * 5) + 3, newColor);
                            img.SetPixel((y * 5), (x * 5) + 4, newColor);
                            img.SetPixel((y * 5) + 1, (x * 5) + 4, newColor);
                            img.SetPixel((y * 5) + 2, (x * 5) + 4, newColor);
                            img.SetPixel((y * 5) + 3, (x * 5) + 4, newColor);
                            img.SetPixel((y * 5) + 4, (x * 5) + 4, newColor);
                        }
                        else if (numericUpDown2.Value == 6)
                        {
                            img.SetPixel((y * 6), (x * 6), newColor);
                            img.SetPixel((y * 6) + 1, (x * 6), newColor);
                            img.SetPixel((y * 6) + 2, (x * 6), newColor);
                            img.SetPixel((y * 6) + 3, (x * 6), newColor);
                            img.SetPixel((y * 6) + 4, (x * 6), newColor);
                            img.SetPixel((y * 6) + 5, (x * 6), newColor);
                            img.SetPixel((y * 6), (x * 6) + 1, newColor);
                            img.SetPixel((y * 6) + 1, (x * 6) + 1, newColor);
                            img.SetPixel((y * 6) + 2, (x * 6) + 1, newColor);
                            img.SetPixel((y * 6) + 3, (x * 6) + 1, newColor);
                            img.SetPixel((y * 6) + 4, (x * 6) + 1, newColor);
                            img.SetPixel((y * 6) + 5, (x * 6) + 1, newColor);
                            img.SetPixel((y * 6), (x * 6) + 2, newColor);
                            img.SetPixel((y * 6) + 1, (x * 6) + 2, newColor);
                            img.SetPixel((y * 6) + 2, (x * 6) + 2, newColor);
                            img.SetPixel((y * 6) + 3, (x * 6) + 2, newColor);
                            img.SetPixel((y * 6) + 4, (x * 6) + 2, newColor);
                            img.SetPixel((y * 6) + 5, (x * 6) + 2, newColor);
                            img.SetPixel((y * 6), (x * 6) + 3, newColor);
                            img.SetPixel((y * 6) + 1, (x * 6) + 3, newColor);
                            img.SetPixel((y * 6) + 2, (x * 6) + 3, newColor);
                            img.SetPixel((y * 6) + 3, (x * 6) + 3, newColor);
                            img.SetPixel((y * 6) + 4, (x * 6) + 3, newColor);
                            img.SetPixel((y * 6) + 5, (x * 6) + 3, newColor);
                            img.SetPixel((y * 6), (x * 6) + 4, newColor);
                            img.SetPixel((y * 6) + 1, (x * 6) + 4, newColor);
                            img.SetPixel((y * 6) + 2, (x * 6) + 4, newColor);
                            img.SetPixel((y * 6) + 3, (x * 6) + 4, newColor);
                            img.SetPixel((y * 6) + 4, (x * 6) + 4, newColor);
                            img.SetPixel((y * 6) + 5, (x * 6) + 4, newColor);
                            img.SetPixel((y * 6), (x * 6) + 5, newColor);
                            img.SetPixel((y * 6) + 1, (x * 6) + 5, newColor);
                            img.SetPixel((y * 6) + 2, (x * 6) + 5, newColor);
                            img.SetPixel((y * 6) + 3, (x * 6) + 5, newColor);
                            img.SetPixel((y * 6) + 4, (x * 6) + 5, newColor);
                            img.SetPixel((y * 6) + 5, (x * 6) + 5, newColor);
                        }
                    }
                    catch { };
                }
            }
            Invoke((Action)(() =>
            {
                if (listBox1.SelectedIndex == 0)
                {
                    label6.Text = "Coldest value: " + gen_median;
                }
                else if (listBox1.SelectedIndex == 1)
                {
                    label6.Text = "Heatest value: " + gen_median;
                }
                else if (listBox1.SelectedIndex == 2)
                {
                    label6.Text = "Median value: " + gen_median;
                }
                else if (listBox1.SelectedIndex == 3)
                {
                    label6.Text = "";
                }

                label1.Text = "" + trackBar1.Value;

                pictureBox1.Image = img;
            }));
        }
        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown2.Value == 1)
            {
                pictureBox1.Width = 80;
                pictureBox1.Height = 60;
            }
            else if (numericUpDown2.Value == 2)
            {
                pictureBox1.Width = 160;
                pictureBox1.Height = 120;
            }
            else if (numericUpDown2.Value == 3)
            {
                pictureBox1.Width = 240;
                pictureBox1.Height = 180;
            }
            else if (numericUpDown2.Value == 4)
            {
                pictureBox1.Width = 320;
                pictureBox1.Height = 240;
            }
            else if (numericUpDown2.Value == 5)
            {
                pictureBox1.Width = 400;
                pictureBox1.Height = 300;
            }
            else if (numericUpDown2.Value == 6)
            {
                pictureBox1.Width = 80 * 6;
                pictureBox1.Height = 60 * 6;
            }
        }
    }
}