using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace IFT585_TP1
{
    public class CoucheMAC
    {

        //now char[] instead of string, this is more efficient
        //private BlockingCollection<string> m_physiqueStreamIn;
        //public BlockingCollection<string> PhysiqueStreamIn

        private BlockingCollection<char[]> m_physiqueStreamIn;
        public BlockingCollection<char[]> PhysiqueStreamIn
        {
            get { return m_physiqueStreamIn; }
        }

        private BlockingCollection<char[]> m_physiqueStreamOut;
        public BlockingCollection<char[]> PhysiqueStreamOut
        {
            get { return m_physiqueStreamOut; }
        }

        private BlockingCollection<Trame> m_LLCStreamIn;
        private BlockingCollection<Trame> m_LLCStreamOut;
        private BlockingCollection<TypeEvenement> m_evenementStream;

        private Signal m_signal;

        public CoucheMAC(Signal signal, CoucheLLC coucheLLC) 
        {
            this.m_signal = signal;
            this.m_evenementStream = coucheLLC.EvenementStream;
            this.m_LLCStreamIn = coucheLLC.MACStreamOut;
            this.m_LLCStreamOut = coucheLLC.MACStreamIn;

            this.m_physiqueStreamIn = new BlockingCollection<char[]>();
            this.m_physiqueStreamOut = new BlockingCollection<char[]>();
        }

        public void envoie_trame(Trame completeFrame) {

            string log_str = "envoie_trame T= " + Thread.CurrentThread.Name + " noTrame: " + completeFrame.NoSequence;
            Logging.log(TypeConsolePrint.All, log_str);

            //First, need to take the bytes[] from that frame and turn them into a series of 0101010101 strings we will be hamming on
            string binrep = bytes_to_bin_string(completeFrame);

            //Call hamming - methode to be writtent
            char[] binrep_with_hamming = insert_hamming_codes(binrep);

            //m_physiqueStreamOut.Add(completeFrame);
            //testing my reconstructed frame from bin
            m_physiqueStreamOut.Add(binrep_with_hamming);

        }

        public void reception_trame(char[] cArray)
        {
            //param is now char[] instead of string.  
            Trame dum = new Trame();

            //new function - check the hamming codes on cArray, returns a the conversion back into string
            //then we can conver to a trame() again with the binRep (string version)
            string binRep = remove_hamming_bits(cArray);

            dum = binString_to_trame(binRep);
            m_LLCStreamOut.Add(dum);

            string log_str = "reception_trame from Thread.Name: " + Thread.CurrentThread.Name + " noTrame: " + dum.NoSequence;
            Logging.log(TypeConsolePrint.All, log_str);
            m_evenementStream.Add(TypeEvenement.ArriveeTrame);
        }

        public string remove_hamming_bits(char[] cArray)
        {
            /*Receives c char array, performs hamming, returns cArray edited without hamming*/
            int parity_bits = num_parity_bits(cArray.Length);
            char[] cArray_data = new char[cArray.Length - parity_bits];
            List<char> li = new List<char>();

            for (int i = 0; i < cArray.Length; i++)
            {
                //Check power:, i+1 since position for hamming is index 1, not 0
                bool isPow = is_power_of_two(i + 1);
                if (!isPow)
                {
                    li.Add(cArray[i]);
                }
            }
            cArray_data = li.ToArray();
            /*
            string log_str = "removing_hamming_codes cArray_data: ";
            foreach (char c in cArray_data) {log_str += c.ToString();}
            log_str += Environment.NewLine;
            Logging.log(TypeConsolePrint.Hamming, log_str);
            */

            return new string(cArray_data);
        }

        public char[] insert_hamming_codes(string binrep) {
            /*insert hamming codes in binrep, returns a binrep including codes*/
            /*We currently have 272 bits for data frames, and 16 bits for ack/nack. Means I need to somewhat account for that in hamming*/

            //Assuming frames of 272 bits for now

            //Create a new array[n bits]
            int parity_bits = num_parity_bits(binrep.Length);

            //data bits + parity bits = length of new array
            char[] cArray_total = new char[binrep.Length + parity_bits];

            //easier to use than the string
            char[] cArray_data = new char[binrep.Length];
            cArray_data = binrep.ToCharArray();
            List<char> li = cArray_data.ToList();

            if (binrep.Length<100)
            {
                //
            }

            for (int i = 0; i < cArray_total.Length; i++)
            {
                //each power of 2 is left blank = will be parity bits (1,2,4,8...)

                //Check power:, i+1 since position for hamming is index 1, not 0
                bool isPow = is_power_of_two(i+1);
                if (!isPow)
                {
                    //there is no "l.pop(0)" apparently in c# - that's a bit disappointing
                    cArray_total[i] = li[0];
                        li.RemoveAt(0);
                }
                //if !power_of_two(i+1), then take() next element from binrep and REMOVE IT until brinrep_as_charArray is full
            }
            //returning char[] since the receiver will need a char[] array for the hamming code as well


            return cArray_total;
        }

        public bool is_power_of_two(int x) {
            //SO: thread 600293, Greg Hewgill
            //Tested and it works - nice use of binary & operator
            return (x & (x-1)) == 0;

        }

        public int num_parity_bits(int data_bits)
        {
            if (data_bits > 247)
            {
                return 9;
            }
            else {
                //Means it's a shorter ack/nack fewer bits
                return 5;
            }

        }

        public string bytes_to_bin_string(Trame tr)
        {
            //we take the important stuff from the frame & make a huge bin out of it
            //this means: type (ack/nak/data), Nosequence and Info (if any)

            //First 2 bytes are noSeq, type
            //The rest is actual data
            //TO DO: should also add flags etc. to do thing well. 

            string bin_str =null;
            byte noSeq = Convert.ToByte(tr.NoSequence);
            byte type = Convert.ToByte(tr.Type);
            bin_str += Convert.ToString(noSeq, 2).PadLeft(8, '0') + Convert.ToString(type, 2).PadLeft(8, '0');

            if (tr.Type==TypeTrame.data)
            {
                byte[] data = tr.Info.Buffer;
                foreach (byte by in data)
                {
                    bin_str += Convert.ToString(by, 2).PadLeft(8, '0');
                }
            }
            return bin_str;
        }

        public Trame binString_to_trame(String binRep)
        {
            /*Takes a binRep which was originally a frame, and converts it back into constituants bytes*/
            int numBytes = binRep.Length / 8;
            byte[] trame_bytes = new byte[numBytes];

            for (int i = 0; i < numBytes; i++)
            {
                trame_bytes[i] = Convert.ToByte(binRep.Substring(8 * i, 8), 2);
            }


            Trame trame_from_binrep = new Trame(trame_bytes[0], trame_bytes[1], trame_bytes.Skip(2).Take(numBytes - 2).ToArray());

            return trame_from_binrep;

        }

        public void Run()
        {
            while (true)
            {
                Trame completeFrame = new Trame();

                if (m_LLCStreamIn.TryTake(out completeFrame, 100))
                {
                    /* Début de trame provenant de la sous-couche LLC */
                    // TO DO : Faire le traitement de la sous-couche MAC
                    envoie_trame(completeFrame);

                }

                //now char[] instead of string
                //string binRep_of_trame;

                char[] dummy = null;        //because we can't do  var cArray; or var cArray = null;
                var cArray = dummy;

                if (m_physiqueStreamIn.TryTake(out cArray, 100))
                {
                    /* Trame provenant de la couche physique - binrep */

                    // TO DO : Faire le traitement de la sous-couche MAC
                    reception_trame(cArray);

                }
            }
        }
    }
}
