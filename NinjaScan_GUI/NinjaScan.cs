﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra.Double;

namespace NinjaScan
{
    /// <summary>
    /// Sylphide Protocol
    /// </summary>
    public class Sylphide_Protocol
    {
        public static byte header0 = 0xF7;
        public static byte header1 = 0xE0;
        public byte sequence;
        public byte[] crc16;
    }

    /// <summary>
    /// A page、IMU sensor page
    /// Output: acceralation, gyrosope, pressure, temparature
    /// </summary>
    public class A_Page
    {
        public static byte header = 0x41;
        public static string name = "A";

        // Sylphide形式によってもunsigned, signed, endianは違うので注意
        public UInt32 inner_time;
        public UInt32 gps_time;
        public UInt32 ax, ay, az;
        public UInt32 gx, gy, gz;
        public UInt32 pressure, temp_press;
        public Int16 temp_gyro;

        public double cal_ax, cal_ay, cal_az;
        public double cal_gx, cal_gy, cal_gz;

        public class CalibrationData
        {
            public double zero_ax, zero_ay, zero_az;
            public double zero_gx, zero_gy, zero_gz;

            //driftは物理量単位
            public double drift_ax, drift_ay, drift_az;
            public double drift_gx, drift_gy, drift_gz;

            public double fullScale_gyro, fullScale_acc;

            public double lsb_gyro, lsb_acc;
        };

        public static CalibrationData defaultCalibrationData;

        public CalibrationData calibrationData = defaultCalibrationData;

        static A_Page()
        {
            defaultCalibrationData = new CalibrationData();

            defaultCalibrationData.zero_ax = Math.Pow(2, 16) / 2;
            defaultCalibrationData.zero_ay = Math.Pow(2, 16) / 2;
            defaultCalibrationData.zero_az = Math.Pow(2, 16) / 2;
            defaultCalibrationData.zero_gx = Math.Pow(2, 16) / 2;
            defaultCalibrationData.zero_gy = Math.Pow(2, 16) / 2;
            defaultCalibrationData.zero_gz = Math.Pow(2, 16) / 2;

            defaultCalibrationData.drift_ax = 0.0; //driftは物理量単位
            defaultCalibrationData.drift_ay = 0.0;
            defaultCalibrationData.drift_az = 0.0;
            defaultCalibrationData.drift_gx = 0.0;
            defaultCalibrationData.drift_gy = 0.0;
            defaultCalibrationData.drift_gz = 0.0;

            defaultCalibrationData.fullScale_gyro = 2000; //Full Scale ±2000dps
            defaultCalibrationData.fullScale_acc = 8; // Full Scale ±8G

            defaultCalibrationData.lsb_gyro = 1.0 / Math.Pow(2, 16) * defaultCalibrationData.fullScale_gyro * 2;
            defaultCalibrationData.lsb_acc = 1.0 / Math.Pow(2, 16) * defaultCalibrationData.fullScale_acc * 2;
        }

        public void Update(BinaryReader input)
        {
            // Windows is little endian
            inner_time = Convert.ToByte(input.ReadByte());
            gps_time = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            ax = Read_3byte_BigEndian(input.ReadBytes(3));
            ay = Read_3byte_BigEndian(input.ReadBytes(3));
            az = Read_3byte_BigEndian(input.ReadBytes(3));
            gx = Read_3byte_BigEndian(input.ReadBytes(3));
            gy = Read_3byte_BigEndian(input.ReadBytes(3));
            gz = Read_3byte_BigEndian(input.ReadBytes(3));
            pressure = Read_3byte_BigEndian(input.ReadBytes(3)); //すごいロガーではNo Data
            temp_press = Read_3byte_BigEndian(input.ReadBytes(3)); //すごいロガーではNo Data
            temp_gyro = BitConverter.ToInt16(input.ReadBytes(2), 0);
            Convert_A_page(calibrationData);
        }

        private static UInt32 Read_3byte_BigEndian(byte[] data)
        {
            //Big Endianな３バイトのデータをBitConverterできるように４バイトのリトルエンディアンな
            //byte[] 配列に変換する
            byte[] buffer = { data[2], data[1], data[0], 0 };
            return BitConverter.ToUInt32(buffer, 0);
        }

        private void Convert_A_page(CalibrationData calib)
        {
            cal_ax = (ax - calib.zero_ax) * calib.lsb_acc - calib.drift_ax;
            cal_ay = (ay - calib.zero_ay) * calib.lsb_acc - calib.drift_ay;
            cal_az = (az - calib.zero_az) * calib.lsb_acc - calib.drift_az;
            cal_gx = (gx - calib.zero_gx) * calib.lsb_gyro - calib.drift_gx;
            cal_gy = (gy - calib.zero_gy) * calib.lsb_gyro - calib.drift_gy;
            cal_gz = (gz - calib.zero_gz) * calib.lsb_gyro - calib.drift_gz;
        }

    }

    /// <summary>
    /// M page、MagneticField
    /// Output: Magnetic Field
    /// Caution: xyz diffent order on product. Ninja-Scan-Lite is x-y-z order.
    /// </summary>
    public class M_Page
    {
        public static byte header = 0x4d;
        public static string name = "M";

        // すごいロガーとその他Sylphide形式のものではxyzの順番が違うので気をつけること
        public UInt16 inner_time;
        public UInt32 gps_time;
        public Int16 mx, my, mz; // uT
        // OUTPUT data range is -30000 to +300000, FullScale is +-1000uT

        public double cal_mx, cal_my, cal_mz;

        public class CalibrationData
        {
            public double zero_mx, zero_my, zero_mz;

            public double fullScale_mag;
            public double lsb_mag;
        };

        public static CalibrationData defaultCalibrationData;

        public CalibrationData calibrationData = defaultCalibrationData;

        static M_Page()
        {
            defaultCalibrationData = new CalibrationData();

            defaultCalibrationData.zero_mx = 0;
            defaultCalibrationData.zero_my = 0;
            defaultCalibrationData.zero_mz = 0;

            defaultCalibrationData.fullScale_mag = 1000; // Full Scale ±1000uT
            defaultCalibrationData.lsb_mag = 0.1; // sensitivity 0.1uT/LSB
        }

        public void Update(BinaryReader input)
        {
            input.ReadBytes(2);
            inner_time = Convert.ToByte(input.ReadByte());
            gps_time = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            mx = Read_2byte_BigEndian(input.ReadBytes(2));
            my = Read_2byte_BigEndian(input.ReadBytes(2));
            mz = Read_2byte_BigEndian(input.ReadBytes(2));
            input.ReadBytes(18);
            Convert_M_page(calibrationData);
        }

        private void Convert_M_page(CalibrationData calib)
        {
            cal_mx = (mx - calib.zero_mx) * calib.lsb_mag;
            cal_my = (my - calib.zero_my) * calib.lsb_mag;
            cal_mz = (mz - calib.zero_mz) * calib.lsb_mag;
        }

        private static Int16 Read_2byte_BigEndian(byte[] data)
        {
            //Big Endianな2バイトのデータをBitConverterできるように2バイトのリトルエンディアンな
            //byte[] 配列に変換する
            byte[] buffer = { data[1], data[0] };
            Int16 output = BitConverter.ToInt16(buffer, 0);
            return output;
            //return BitConverter.ToUInt16(buffer, 0);
        }

        /// <summary>
        /// http://www.fujikura.co.jp/rd/gihou/backnumber/pages/__icsFiles/afieldfile/2012/11/08/122_R8.pdf
        /// 
        /// </summary>
        private void Calibration()
        {
            // オフセット、全磁力を初期化
            double offset_x = 0;
            double offset_y = 0;
            double offset_z = 0;
            double total_magnetic_force = 0; // 全磁力
            double alpha = 1.0;

            // 共分散行列を初期化
            DenseMatrix covariance = new DenseMatrix(4, 4, new[] {alpha, 0, 0, 0,
                                                                  0, alpha, 0, 0,
                                                                  0, 0, alpha, 0,
                                                                  0, 0, 0, alpha});

            // 磁気データとオフセット、全磁力を使用し、誤差関数eを計算する
            double A = Math.Pow(cal_mx - offset_x, 2) +
                       Math.Pow(cal_my - offset_y, 2) +
                       Math.Pow(cal_mz - offset_z, 2);
            double B = Math.Pow(total_magnetic_force, 2);
            double e = A - B;

            // 誤差関数e、磁気データ、共分散行列を使用し、オフセット残渣ηを計算する


            // 磁気データを使用し、共分散行列Pを更新する

            // オフセット残渣ηをオフセットに加算し、最新のオフセットとする


        }
    }

    /// <summary>
    /// P page、Pressure
    /// Output: air pressure, temparature
    /// MS5611 calculation
    /// </summary>
    public class P_Page
    {
        public static byte header = 0x50;
        public static string name = "P";

        public UInt32 inner_time;
        public UInt32 gps_time;
        public Int32 pressure; //hPa*100
        public Int32 temperature;  //deg*100

        public void Update(BinaryReader input)
        {
            input.ReadBytes(2);

            inner_time = Convert.ToByte(input.ReadByte());
            gps_time = BitConverter.ToUInt32(input.ReadBytes(4), 0);

            UInt32 D1 = Read_3byte_BigEndian(input.ReadBytes(3));
            UInt32 D2 = Read_3byte_BigEndian(input.ReadBytes(3));

            input.ReadBytes(6);

            UInt32 coef1 = Read_2byte_BigEndian(input.ReadBytes(2));
            UInt32 coef2 = Read_2byte_BigEndian(input.ReadBytes(2));
            UInt32 coef3 = Read_2byte_BigEndian(input.ReadBytes(2));
            UInt32 coef4 = Read_2byte_BigEndian(input.ReadBytes(2));
            UInt32 coef5 = Read_2byte_BigEndian(input.ReadBytes(2));
            UInt32 coef6 = Read_2byte_BigEndian(input.ReadBytes(2));
            Int64 dT = (Int32)(D2 - coef5 * Math.Pow(2, 8));
            temperature = (Int32)(2000 + dT * coef6 / Math.Pow(2, 23));
            Int64 OFF = (Int64)(coef2 * Math.Pow(2, 16) + (coef4 * dT) / Math.Pow(2, 7));
            Int64 SENS = (Int64)(coef1 * Math.Pow(2, 15) + (coef3 * dT) / Math.Pow(2, 8));
            pressure = (Int32)((D1 * SENS / Math.Pow(2, 21) - OFF) / Math.Pow(2, 15));
        }

        private static UInt32 Read_3byte_BigEndian(byte[] data)
        {
            //Big Endianな３バイトのデータをBitConverterできるように４バイトのリトルエンディアンな
            //byte[] 配列に変換する
            byte[] buffer = { data[2], data[1], data[0], 0 };
            return BitConverter.ToUInt32(buffer, 0);
        }

        private static UInt16 Read_2byte_BigEndian(byte[] data)
        {
            //Big Endianな2バイトのデータをBitConverterできるように2バイトのリトルエンディアンな
            //byte[] 配列に変換する
            byte[] buffer = { data[1], data[0] };
            return BitConverter.ToUInt16(buffer, 0);
        }
    }

    /// <summary>
    /// H page、HumanPoweredAirplane
    /// Output: counter of cadace meter, counter of airspeed meter, altitude, control stick, auxually
    /// </summary>
    public class H_Page
    {
        public static byte header = 0x48;
        public static string name = "H";

        public UInt32 inner_time;
        public UInt32 gps_time;
        public UInt16 cadence_counter;
        public UInt16 airspeed_counter;
        public UInt16 altimeter;
        public UInt16 control_stick1;
        public UInt16 control_stick2;
        public UInt16 control_stick3;
        public UInt16 aux1, aux2, aux3, aux4;

        public void Update(BinaryReader input)
        {
            input.ReadBytes(2);
            inner_time = Convert.ToByte(input.ReadByte());
            gps_time = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            cadence_counter = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            airspeed_counter = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            altimeter = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            control_stick1 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            control_stick2 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            control_stick3 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            aux1 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            aux2 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            aux3 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            aux4 = BitConverter.ToUInt16(input.ReadBytes(2), 0);
        }
    }

    /// <summary>
    /// N page、Navigation
    /// Output: latitude, longitude, altitude, velocity, attitude
    /// </summary>
    public class N_Page
    {
        public static byte header = 0x4E;
        public static string name = "N";

        public UInt32 sequence_num;
        public UInt32 gps_time;
        public UInt32 latitude;
        public UInt32 longitude;
        public UInt32 altitude;
        public UInt16 velocity_north;
        public UInt16 velocity_east;
        public UInt16 velocity_down;
        public UInt16 heading;
        public UInt16 roll;
        public UInt16 pitch;

        public void Update(BinaryReader input)
        {
            sequence_num = Convert.ToByte(input.ReadByte());
            input.ReadBytes(2);
            gps_time = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            latitude = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            longitude = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            altitude = BitConverter.ToUInt32(input.ReadBytes(4), 0);
            velocity_north = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            velocity_east = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            velocity_down = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            heading = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            roll = BitConverter.ToUInt16(input.ReadBytes(2), 0);
            pitch = BitConverter.ToUInt16(input.ReadBytes(2), 0);
        }
    }

    /// <summary>
    /// G page、GPS
    /// Output: u-blox format GPS binary data
    /// </summary>
    public class G_Page
    {
        public static byte header = 0x47;
        public static string name = "G";

        public byte[] ubx_raw = new byte[31];
        public byte[] ubx_buf = new byte[0x400];
        public int ubx_buf_length = 0;

        public bool hasOutput = false;

        int ubx_index_header0 = -1, ubx_index_header1 = -1;
        byte[] ubx_id = new byte[2];
        int ubx_payload_length;

        public class UBlox
        {
            public UBX.NAV_POSLLH llh = new UBX.NAV_POSLLH();
            public UBX.NAV_SOL sol = new UBX.NAV_SOL();
            public UBX.NAV_VELNED velned = new UBX.NAV_VELNED();
            public UBX.NAV_STATUS status = new UBX.NAV_STATUS();
            public UBX.NAV_TIMEUTC utc = new UBX.NAV_TIMEUTC();
            public UBX.NMEA nmea = new UBX.NMEA();
        }
        public UBlox ubx = new UBlox();

        public void Update(BinaryReader input)
        {
            int length = input.BaseStream.Read(ubx_raw, 0, ubx_raw.Length);

            if (ubx_buf.Length < ubx_buf_length + ubx_raw.Length) // 溢れ防止
            {
                ubx_buf_length -= ubx_raw.Length;
                Array.Copy(ubx_buf, ubx_raw.Length, ubx_buf, 0, ubx_buf_length);
                ubx_index_header0 = -1;
            }

            Array.Copy(ubx_raw, 0, ubx_buf, ubx_buf_length, length); //31バイトをbuffにコピー,object_offsetがあれば、その分オフセット
            ubx_buf_length += length;
            Parse();
        }

        /// <summary>
        /// ヘッドシークか、ペイロード解析なのか
        /// （ヘッドシーク）ヘッドがあるかどうか ->N/buff廃棄
        /// （ヘッドあり）次のinputが必要か？
        /// （input必要）inputを次の32byteを含めてヘッドシークでやり直し
        /// （input不必要）IDでペイロードの長さを求めてペイロード解析へ。解析対象がさらに必要かどうか？
        /// （buff必要）ペイロード解析としてやり直し
        /// （buff不必要）解析を行う。チェックサム以降はbuffに入れて、append_offsetをチェックサムのindexにする
        /// </summary>
        /// <param name="input"></param>
        private void Parse()
        {
            hasOutput = false; // NAV-VELNED以外の時は出力しない。

            while ((ubx_buf_length - ubx_index_header0) >= 8)
            {
                if (ubx_index_header0 < 0)
                { // ヘッドシーク
                    ubx_index_header0 = Array.IndexOf(ubx_buf, UBX.header[0], 0, ubx_buf_length);
                    if (ubx_index_header0 < 0)
                    {
                        ubx_buf_length = 0;
                        break;
                    }
                    ubx_index_header1 = -1;
                }

                if (ubx_index_header1 < 0)
                {
                    ubx_index_header1 = ubx_index_header0 + 1;
                    ubx_index_header1 = Array.IndexOf(ubx_buf, UBX.header[1], ubx_index_header1, ubx_buf_length - ubx_index_header1);

                    if (ubx_index_header1 < 0)
                    {
                        ubx_index_header0 = -1;
                        ubx_buf_length = 0;
                        break;
                    }

                    if (ubx_index_header0 + 1 != ubx_index_header1)
                    {
                        if (ubx_buf[ubx_index_header1 - 1] != UBX.header[0])
                        {
                            ubx_buf_length -= (ubx_index_header1 + 1);
                            Array.Copy(ubx_buf, ubx_index_header1 + 1, ubx_buf, 0, ubx_buf_length);
                            ubx_index_header0 = -1;
                            continue;
                        }
                        ubx_index_header0 = ubx_index_header1 - 1;
                    }

                    // IDの読み取り
                    Array.Copy(ubx_buf, ubx_index_header0 + 2, ubx_id, 0, 2);

                    // ペイロードの長さの読み取り
                    ubx_payload_length = BitConverter.ToUInt16(ubx_buf, ubx_index_header0 + 4);

                    // ペイロード長さがかなり大きい場合はヘッダを適当なものに読み間違えているので、廃棄
                    if (ubx_payload_length > ubx_buf.Length / 2)
                    {
                        ubx_buf_length -= (ubx_index_header0 + 2);
                        Array.Copy(ubx_buf, ubx_index_header0 + 2, ubx_buf, 0, ubx_buf_length);
                        ubx_index_header0 = -1;
                        continue;
                    }
                }

                if (ubx_index_header0 + ubx_payload_length + 8 > ubx_buf_length) // サイズ不足
                {
                    break;
                }

                //byte[] b = new byte[ubx_payload_length + 8];
                //Array.Copy(ubx_buf, ubx_index_header0, b, 0, ubx_payload_length + 8);
                //MessageBox.Show(BitConverter.ToString(b));

                // IDによる場合分け
                if (ubx_id.SequenceEqual(UBX.id_NAV_POSLLH))
                {
                    ubx.llh.Update(ubx_buf, ubx_index_header0);
                }
                else if (ubx_id.SequenceEqual(UBX.id_NAV_STATUS))
                {
                    ubx.status.Update(ubx_buf, ubx_index_header0);
                }
                else if (ubx_id.SequenceEqual(UBX.id_NAV_SOL))
                {
                    ubx.sol.Update(ubx_buf, ubx_index_header0);
                }
                else if (ubx_id.SequenceEqual(UBX.id_NAV_VELNED))
                {
                    ubx.velned.Update(ubx_buf, ubx_index_header0);
                    ubx.nmea.Update(ubx.llh, ubx.sol, ubx.status);
                    hasOutput = true;
                }
                else if (ubx_id.SequenceEqual(UBX.id_NAV_TIMEUTC))
                {
                    ubx.utc.Update(ubx_buf, ubx_index_header0);
                }
                else if (ubx_id.SequenceEqual(UBX.id_NAV_SVINFO))
                {

                }

                ubx_index_header0 += ubx_payload_length + 8;
                ubx_buf_length -= ubx_index_header0;
                Array.Copy(ubx_buf, ubx_index_header0, ubx_buf, 0, ubx_buf_length);

                ubx_index_header0 = -1;
            }
        }
    }

    public class UBX
    {
        public static byte[] header = { 0xB5, 0x62 };
        public static byte[] id_NAV_POSLLH = { 0x01, 0x02 };
        public static byte[] id_NAV_STATUS = { 0x01, 0x03 };
        public static byte[] id_NAV_DOP = { 0x01, 0x04 };
        public static byte[] id_NAV_SOL = { 0x01, 0x06 };
        public static byte[] id_NAV_VELNED = { 0x01, 0x12 };
        public static byte[] id_NAV_TIMEGPS = { 0x01, 0x20 };
        public static byte[] id_NAV_TIMEUTC = { 0x01, 0x21 };
        public static byte[] id_NAV_SVINFO = { 0x01, 0x30 };
        public static byte[] id_RXM_RAW = { 0x02, 0x10 };
        public static byte[] id_RXM_SFRB = { 0x02, 0x31 };
        public static byte[] id_RXM_EPH = { 0x02, 0x31 };
        public static byte[] id_AID_HUI = { 0x0B, 0x02 };

        public class NAV_POSLLH
        {
            public UInt32 itow;
            public Int32 lon, lat, height; //緯度経度高度 緯度経度はe-7 deg 高度はmm
            public Int32 hMSL; //平均海面高度
            public UInt32 hAcc, vAcc; //推定精度

            public void Update(byte[] input, int index_header0)
            {
                itow = BitConverter.ToUInt32(input, index_header0 + 6);
                lon = BitConverter.ToInt32(input, index_header0 + 10);
                lat = BitConverter.ToInt32(input, index_header0 + 14);
                height = BitConverter.ToInt32(input, index_header0 + 18);
                hMSL = BitConverter.ToInt32(input, index_header0 + 22);
                hAcc = BitConverter.ToUInt32(input, index_header0 + 26);
                vAcc = BitConverter.ToUInt32(input, index_header0 + 30);
            }
        }

        public class NAV_SOL
        {
            public UInt32 itow;
            public Int32 ftow;
            public Int16 week;
            public byte flags;
            public Int32 ecefX;
            public Int32 ecefY;
            public Int32 ecefZ;
            public UInt32 pAcc;
            public Int32 ecefVX;
            public Int32 ecefVY;
            public Int32 ecefVZ;
            public UInt32 sAcc;
            public UInt16 pDOP;
            public byte numSV;

            public void Update(byte[] input, int index_header0)
            {
                itow = BitConverter.ToUInt32(input, index_header0 + 6);
                ftow = BitConverter.ToInt32(input, index_header0 + 10);
                week = BitConverter.ToInt16(input, index_header0 + 14);
                //gpsFix; index_header0 + 16
                //flags; index_header0 + 17
                ecefX = BitConverter.ToInt32(input, index_header0 + 18);
                ecefX = BitConverter.ToInt32(input, index_header0 + 22);
                ecefX = BitConverter.ToInt32(input, index_header0 + 26);
                pAcc = BitConverter.ToUInt32(input, index_header0 + 30);
                ecefVX = BitConverter.ToInt32(input, index_header0 + 34);
                ecefVX = BitConverter.ToInt32(input, index_header0 + 38);
                ecefVX = BitConverter.ToInt32(input, index_header0 + 42);
                sAcc = BitConverter.ToUInt32(input, index_header0 + 46);
                pDOP = BitConverter.ToUInt16(input, index_header0 + 50);
                numSV = input[index_header0 + 53];
            }
        }

        public class NAV_VELNED
        {
            public UInt32 itow;
            public Int32 velN; // cm/s
            public Int32 velE;
            public Int32 velD;
            public UInt32 speed; // 3-D speed cm/s
            public UInt32 gSpeed; // ground speed cm/s
            public Int32 heaading; // Heading of motion 2-D 1e-5 deg
            public UInt32 sAcc, cAcc;

            public void Update(byte[] input, int index_header0)
            {
                itow = BitConverter.ToUInt32(input, index_header0 + 6);
                velN = BitConverter.ToInt32(input, index_header0 + 10);
                velE = BitConverter.ToInt32(input, index_header0 + 14);
                velD = BitConverter.ToInt32(input, index_header0 + 18);
                speed = BitConverter.ToUInt32(input, index_header0 + 22);
                gSpeed = BitConverter.ToUInt32(input, index_header0 + 26);
                heaading = BitConverter.ToInt32(input, index_header0 + 30);
                sAcc = BitConverter.ToUInt32(input, index_header0 + 34);
                cAcc = BitConverter.ToUInt32(input, index_header0 + 38);
            }
        }

        public class NAV_STATUS
        {
            public UInt32 itow;
            public byte status_flags;
            public byte fixStat;
            public byte status_flags2;
            public UInt32 ttff;
            public UInt32 msss;
            public string gpsFix; //0x00=no fix, 0x01=dead reckoning only, 0x02=2d fix, 0x03=3d fix, 0x05=time only fix

            public static byte[] gpsFix_NoFix = new byte[1] { 0x00 };
            public static byte[] gpsFix_DeadReckoningOnly = new byte[1] { 0x01 };
            public static byte[] gpsFix_2DFix = new byte[1] { 0x02 };
            public static byte[] gpsFix_3DFix = new byte[1] { 0x03 };
            public static byte[] gpsFix_GPSDeadReckoningConbined = new byte[1] { 0x04 };
            public static byte[] gpsFix_TimeOnlyFix = new byte[1] { 0x05 };

            public void Update(byte[] input, int index_header0)
            {
                byte[] bytegpsFix = new byte[1];
                byte[] byteflags = new byte[1];
                byte[] bytefixStat = new byte[1];
                byte[] byteflags2 = new byte[1];

                Array.Copy(input, index_header0 + 10, bytegpsFix, 0, 1);
                Array.Copy(input, index_header0 + 11, byteflags, 0, 1);
                Array.Copy(input, index_header0 + 12, bytefixStat, 0, 1);
                Array.Copy(input, index_header0 + 13, byteflags2, 0, 1);

                itow = BitConverter.ToUInt32(input, index_header0 + 6);
                ttff = BitConverter.ToUInt32(input, index_header0 + 14);
                msss = BitConverter.ToUInt32(input, index_header0 + 18);
                if (bytegpsFix.SequenceEqual(gpsFix_NoFix))
                {
                    gpsFix = "No Fix";
                }
                else if (bytegpsFix.SequenceEqual(gpsFix_2DFix))
                {
                    gpsFix = "2D Fix";
                }
                else if (bytegpsFix.SequenceEqual(gpsFix_3DFix))
                {
                    gpsFix = "3D Fix";
                }
                else if (bytegpsFix.SequenceEqual(gpsFix_TimeOnlyFix))
                {
                    gpsFix = "Time Only Fix";
                }
            }
        }

        public class NAV_TIMEUTC
        {
            public UInt32 itow;
            public UInt16 year;
            public byte month;
            public byte day;
            public byte hour;
            public byte min;
            public byte sec;

            public void Update(byte[] input, int index_header0)
            {
                itow = BitConverter.ToUInt32(input, index_header0 + 6);
                year = BitConverter.ToUInt16(input, index_header0 + 18);
                month = input[index_header0 + 20];
                day = input[index_header0 + 21];
                hour = input[index_header0 + 22];
                min = input[index_header0 + 23];
                sec = input[index_header0 + 24];
            }
        }

        // Time
        public class TOWTime
        {
            public UInt32 tow_day;
            public UInt32 tow_hour;
            public UInt32 tow_min;
            public double tow_sec;

            // itow(=Time of Weeks)から時刻を求める
            // http://www.novatel.com/support/knowledge-and-learning/published-papers-and-documents/unit-conversions/
            public void Update(UInt32 iToW)
            {
                double remainder_tow_day, remainder_tow_hour, remainder_tow_min;
                tow_day = iToW / 1000 / 86400;
                remainder_tow_day = (double)(iToW) / 1000.0 / 86400.0 - tow_day;
                tow_hour = (UInt32)(remainder_tow_day * 86400 / 3600);
                remainder_tow_hour = (remainder_tow_day * 86400 / 3600) - tow_hour;
                tow_min = (UInt32)(remainder_tow_hour * 3600 / 60);
                remainder_tow_min = (remainder_tow_hour * 3600 / 60) - tow_min;
                tow_sec = remainder_tow_min * 60;
            }
        }

        public class NMEA
        {
            public DateTime time;
            public Double utc;
            public Double lat;
            public Double lon;
            public Double fixquality;
            public Double numSV;
            public Double hDOP;
            public Double alt;
            public Double height_geoid;
            public Double day;
            public Double month;
            public Double year;
            public Double hour;
            public Double min;
            public string gpgga;
            public string gpzda;

            public void Update(NAV_POSLLH llh, NAV_SOL sol, NAV_STATUS status)
            {
                time = GetFromGps(sol.week, sol.itow);
                utc = time.Hour * 10000 + time.Minute * 100 + time.Second + time.Millisecond / 1000.0;
                lat = deg2gpsformat(llh.lat / Math.Pow(10, 7));
                lon = deg2gpsformat(llh.lon / Math.Pow(10, 7));
                if (status.gpsFix == NAV_STATUS.gpsFix_NoFix.ToString()
                    || status.gpsFix == NAV_STATUS.gpsFix_TimeOnlyFix.ToString())
                {
                    fixquality = 0;
                }
                else
                {
                    fixquality = 1;
                }
                numSV = sol.numSV;
                hDOP = sol.pDOP / 100.0;
                alt = llh.hMSL / 1000.0;
                height_geoid = (llh.height - llh.hMSL) / 1000.0;
                gpgga = make_GGA();
                day = time.Day;
                month = time.Month;
                year = time.Year;
                hour = time.Hour;
                min = time.Minute;
                gpzda = make_ZDA();
            }

            private static double deg2gpsformat(double pos)
            {
                // 緯度経度(deg)からNMEAのセンテンス形式に変換
                // ex.緯度48.1167***度をNEMAフォーマットの4807.03***の分刻みに変換
                // [DD.DDDDDDDD]->[DDMM.MMMMMM](D:度,M:分)
                int degree = (int)Math.Floor(pos);
                float minute = (float)((pos - degree) * 60);
                return degree * 100 + minute;
            }

            private static DateTime GetFromGps(Int16 weeknumber, UInt32 msec)
            {
                // gpsweekとitowから時刻生成
                DateTime datum = new DateTime(1980, 1, 6, 0, 0, 0);
                DateTime week = datum.AddDays(weeknumber * 7);
                DateTime time = week.AddMilliseconds(msec);
                return time;
            }

            private static string make_checksum(string str)
            {
                // NMEAセンテンスのチェックサム作成
                // '$GPGGA,~~~,M,,0000*'までの文字列を読み込んでチェックサム(8bitの16進数表記0x**)出力
                // “$”、”!”、”*”を含まないセンテンス中の全ての文字 の8ビットの排他的論理和。","は含むので注意
                // ex. $GPGGA,125044.001,3536.1985,N,13941.0743,E,2,09,1.0,12.5,M,36.1,M,,0000*6A
                int num_str = str.Length;
                byte checksum = 0;
                for (int i = 0; i < num_str; i++)
                {
                    if (str[i] != '$' && str[i] != '!' && str[i] != '*')
                    {
                        checksum = (byte)(checksum ^ (byte)str[i]);
                    }
                }
                return checksum.ToString("X2");
            }

            private string make_GGA()
            {
                // GPGGAセンテンス生成
                string gga;
                if (numSV == 0)
                {
                    return "$GPGGA,,N,,E,0,00,,,M,,M,,*41";
                }
                gga = string.Format("$GPGGA,{0:000000.00},{1:0000.000000},", utc, lat);
                if (lat > 0)
                {
                    gga += string.Format("N,");
                }
                else
                {
                    gga += string.Format("S,");
                }
                gga += string.Format("{0:00000.000000},", lon);
                if (lon > 0)
                {
                    gga += string.Format("E,");
                }
                else
                {
                    gga += string.Format("W,");
                }
                gga += string.Format("{0:0},{1:00},{2:0.0},{3:N2},M,{4:N2},M,,*",
                    fixquality, numSV, hDOP, alt, height_geoid);
                return gga + make_checksum(gga);
            }

            private string make_ZDA()
            {
                // GPZDAセンテンス生成
                string zda;
                if (time.Year == 1980)
                {
                    return "$GPZDA,,,,,,*48";
                }

                zda = string.Format("$GPZDA,{0:000000.00},{1:00},{2:00},{3:0000},{4:00},{5:00}*",
                    utc, day, month, year, hour, min);
                return zda + make_checksum(zda);
            }
        }
    }
}
