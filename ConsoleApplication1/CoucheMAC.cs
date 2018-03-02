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

            //string log_str = "reception_trame from Thread.Name: " + Thread.CurrentThread.Name + " noTrame: " + dum.NoSequence;
            string log_str = "receive " + new string(cArray);
            //Logging.log(TypeConsolePrint.Hamming, log_str);
            m_evenementStream.Add(TypeEvenement.ArriveeTrame);
        }

        public string remove_hamming_bits(char[] cArray)
        {
            /*Receives c char array, returns cArray edited without hamming*/
            int parity_bits = num_parity_bits(cArray.Length);
            char[] cArray_data = new char[cArray.Length - parity_bits];

            List<char> li = new List<char>();
            List<char> parity_list = new List<char>();

            var test = check_hamming_reception(cArray);


            for (int i = 0; i < cArray.Length; i++)
            {
                //Check power:, i+1 since position for hamming is index 1, not 0
                bool isPow = is_power_of_two(i + 1);
                if (!isPow)
                {
                    li.Add(cArray[i]);
                }
                else
                {
                    parity_list.Add(cArray[i]);
                }
            }
            cArray_data = li.ToArray();

            string log_str = "hamDecodes " + new string(parity_list.ToArray());
            Logging.log(TypeConsolePrint.Hamming, log_str);

            return new string(cArray_data);
        }

        public bool check_hamming_reception(char[] cArray)
        {
            /*THIS MUST be called with parity bits still in*/
            //string log_str = "check " + new string(cArray);
            //Logging.log(TypeConsolePrint.Hamming, log_str);


            //extract the parity bits
            List<char> parity_bits = new List<char>();
            for (int i = 0; Math.Pow(2, i) < cArray.Length; i++)
            {
                int parity_bit_pos = Convert.ToInt32(Math.Pow(2, i));
                parity_bits.Add(cArray[parity_bit_pos-1]);
            }

            //re-create parity bit list based on the data
            List<char> l = new List<char>();
            for (int i = 1; i <= cArray.Length; i++)
            {
                if (is_power_of_two(i))
                {
                    //we check the parity for those powers of 2
                    char parity = check_parity(cArray, i);
                    l.Add(parity);
                }
            }

            //what we built should match what we extracted
            string log_str = "extracte: " + new string(parity_bits.ToArray()) +  Environment.NewLine;
            log_str += "checks ge: " + new string(l.ToArray());

            Logging.log(TypeConsolePrint.Hamming, log_str);
            if (parity_bits.Equals(l))
            {
  
            }
            else
            {

            }

            //string log_str = "hamDecodes " + new string(parity_list.ToArray());
            //Logging.log(TypeConsolePrint.Hamming, log_str);

            return false;
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

            for (int i = 1; i <= cArray_total.Length; i++)
            {
                //each power of 2 is left blank = will be parity bits (1,2,4,8...)
                bool isPow = is_power_of_two(i);
                if (!isPow)
                {
                    //there is no "l.pop(0)" apparently in c# - that's a bit disappointing
                    cArray_total[i-1] = li[0];
                        li.RemoveAt(0);
                }
            }

            //Yeah.... not the best to loop around it twice. It could really be optimized, but then 
            //early optimization of details is the root of all evil. It would also require more brain juice
            //than I have available right now.
            //Calculate parity bits
            //string log_str = "beforHam: " + new string(cArray_data) + Environment.NewLine;
            //log_str += "mid- Ham: " + new string(cArray_total) + Environment.NewLine;

            List<char> l = new List<char>();
            for (int i = 1; i <= cArray_total.Length; i++)
            {
                if (is_power_of_two(i))
                {
                    //we check the parity for those powers of 2
                    char parity = check_parity(cArray_total, i);
                    l.Add(parity);
                    cArray_total[i - 1] = parity;
                }
            }
            //log_str += Environment.NewLine + "afterHam: " + new string(cArray_total);
            //log_str += Environment.NewLine + "hamCodes: " + new string(l.ToArray());

            string log_str = Environment.NewLine + "hamCodes: " + new string(l.ToArray());
            Logging.log(TypeConsolePrint.Hamming, log_str);

            //returning char[] since the receiver will need a char[] array for the hamming code as well
            return cArray_total;

        }



        public char check_parity(char[] c, int index) {
            /*Takes the char[] - full length, along with position, computes the parity bit value for that index, returns the char 1, or 0*/

            int i = index;
            int num_of_ones = 0;
            while (i < c.Length)
            {
                int j = 0;  //this controls the "batch" number
                while (j<index && i + j - 1 < c.Length)     //when j gets to index, this batch is done. Don't want array out of bounds though
                {
                    num_of_ones = c[i + j - 1] == '1' ? num_of_ones +=1 : num_of_ones;
                    j++;
                }

                i += 2*index;      //we skip 1 batch - so we add twice the index value of the batch
            }

            //at this point num_of_ones gives us the parity

            return (num_of_ones % 2).ToString().ToCharArray()[0];
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
