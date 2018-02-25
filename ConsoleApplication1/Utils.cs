using System;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Text;

namespace IFT585_TP1
{
    public static class Constants
    {
        /*
         * Numéro maximal de séquence d'une trame.
         * Les trames émises possèdent un numéro de séquence entre 0 et MAX_SEQ.
         * 
         * Note : Sous la forme (2^n - 1) => peut être codé sur n bits
         */
        public const uint MAX_SEQ = 7;

        /*
         * Taille d'une fenêtre pouvant contenir un numéro de séquence
         */
        public const uint NB_BUFS = (MAX_SEQ + 1) / 2;

        public const uint M = 32;
        public const uint N = 64;

        public const uint TIMEOUT = 1000;
        public const uint ACK_TIMEOUT = 1000;

        public const TypeConsolePrint print_configuration = TypeConsolePrint.SendingPath;

        public const string log_file_path = @"U:\hiver2018\ift585\tp1_liaison\log\log.txt";
        public static readonly object _logObj = new Object();
    }

    public static class Logging {

        public static void log(TypeConsolePrint type, string str_to_print)
        {
            /*This methods filters what we want or not to print. Useful for debugging. */
            if (type == Constants.print_configuration || Constants.print_configuration==TypeConsolePrint.All)
            {
                Console.WriteLine(str_to_print);

                ReaderWriterLock locker = new ReaderWriterLock();

                lock (Constants._logObj)
                {
                    using (StreamWriter sw = File.AppendText(Constants.log_file_path))
                    {
                        sw.Write(str_to_print);
                        sw.Write(Environment.NewLine);
                        sw.Close();
                    }
                }
            }
        }
      
        public static void createLogFile() {
            using (StreamWriter sw = File.CreateText(Constants.log_file_path))
            {
                sw.Write("Debug from run on: " + DateTime.Now.ToString("h:mm:ss"));
                sw.Close();
            }
        }
    }

  


    public enum TypeEvenement
        /*The various thing that can happen which would warrant some response from other threads. Kind of an observer pattern*/
    {
        ArriveeTrame,
        CkSumErr,
        Timeout,
        CoucheReseauPrete,
        ACKTimeout,
        PaquetRecu
    }

    public enum TypeConsolePrint
    { /*To decide what we actually print on the screen... based on what we want to know/debug*/
        ReceptionPath,
        SendingPath,
        All,
        Event

    }

    public enum TypeTrame
    {
        data,
        ack,
        nak
    }

    public class Trame
    {
        public Trame()
        {
            _taille = Constants.N;
        }

        private uint _taille;
        public uint Taille 
        { 
            get { return _taille; } 
            set { _taille = value; } 
        }

        private uint _ack;
        public uint ACK
        {
            get { return _ack; }
            set { _ack = value; }
        }

        private uint _noSeq;
        public uint NoSequence
        { 
            get { return _noSeq; }
            set { _noSeq = value; } 
        }

        private TypeTrame _type;
        public TypeTrame Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private Paquet _information;
        public Paquet Info
        { 
            get { return _information; } 
            set { _information = value; } 
        }

        public override string ToString()
        {
            return $"noSequence:{_noSeq}, _taille:{_taille}, _ack:{_ack}, _type:{_type}, _information:{_information}";
        }
    }

    public class Paquet
    {
        private byte[] _buffer;
        private uint _taille;

        public Paquet()
        {
            _taille = Constants.M;
            _buffer = new byte[_taille];
        }

        public Paquet(uint var)
        {
            _taille = var;
            _buffer = new byte[_taille];
        }

        public uint Taille => _taille;
        public byte[] Buffer => _buffer;

        public override string ToString()
        {
            
            return $"{Encoding.Default.GetString(_buffer)}";
        }
    }
}
