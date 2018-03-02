using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace IFT585_TP1
{
    public class CoucheMAC
    {
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

            //First, need to take the bytes[] from that frame and turn them into a series of 0101010101 char[] we will be hamming on
            char[] binrep_with_hamming = insert_hamming_codes(bytes_to_bin_string(completeFrame));

            m_physiqueStreamOut.Add(binrep_with_hamming);

        }

        public void reception_trame(char[] cArray)
        {
            Trame dum = new Trame();
            List<List<char>> vals = new List<List<char>>();
        
            //Removing the hamming codes from the message
            vals = remove_hamming_bits(cArray);

            //Check if message is correct, correct errors there if not. This returns the corrected message
            //or the same message if there was no error
            char[] correct_charArray = check_hamming_reception(vals, cArray);

            //the constructor in Trame class needs a string represenation to work
            dum = binString_to_trame(new string(correct_charArray));
            m_LLCStreamOut.Add(dum);

            m_evenementStream.Add(TypeEvenement.ArriveeTrame);
        }

        public List<List<char>> remove_hamming_bits(char[] cArray)
        {
            /*Receives c char array, returns 1 list<char>. [0] contains the data without the hamming codes
            [1] contains the hamming codes received  in the message so they can be checked*/
            int parity_bits = num_parity_bits(cArray.Length);
            char[] cArray_data = new char[cArray.Length - parity_bits];

            List<char> data = new List<char>();
            List<char> parity_list = new List<char>();
            List<List<char>> ret = new List<List<char>>();

            for (int i = 0; i < cArray.Length; i++)
            {
                //Check power:, i+1 since position for hamming is index 1, not 0
                bool isPow = is_power_of_two(i + 1);
                if (!isPow)
                {
                    data.Add(cArray[i]);
                }
                else
                {
                    parity_list.Add(cArray[i]);
                }
            }

            ret.Add(data);
            ret.Add(parity_list);
            return ret;
        }

        public char[] check_hamming_reception(List<List<char>> li, char[] cArray_full)
        {
            /*Checks if the data (li[0]) matchs the parity bits (li[1])*/

            List<char> parity_bits =  li[1];
            List<char> data_bits = li[0];
            List<List<char>> ret = new List<List<char>>();
            char[] corrected_data;
            string log_str = "";

            //Use data_bits to re-construct the hamming codes, as if we were sending the data again 
            char[] cArray = insert_hamming_codes(new string(data_bits.ToArray()));
            ret = remove_hamming_bits(cArray);

            //the parity bits we just calculated should match those that came with the message, parity_bits
            if (ret[1].SequenceEqual(parity_bits))
            {
                //They are == meaning there was no error in the message
                return data_bits.ToArray();
            }
            else
            {
                //error detected - need to threat it
                corrected_data = threat_error_detected(parity_bits, ret[1], cArray_full);
                log_str = "parity1: " + new string(parity_bits.ToArray()) + Environment.NewLine;
                //log_str += "allvect: " + new string(cArray) + Environment.NewLine;
                log_str += "databfr: " + new string(data_bits.ToArray()) + Environment.NewLine;
                log_str += "parity2: " + new string(ret[1].ToArray()) + Environment.NewLine;
                log_str += "dataaft: " + new string(corrected_data) + Environment.NewLine;
                Logging.log(TypeConsolePrint.Hamming, log_str);

                return corrected_data;

            }


        }

        private char[] threat_error_detected(List<char> h1_bits, List<char> h2_bits, char[] cArray_full)
        {
            //First, get the syndrome - that' binary addition of parity of h1 (the parity bits that came with the message
            //and h2 - the parity bits we recalculated. We add them in binary., then we reverse it.
            //This happens (because of maths) to be the binary representation of the position of the mistake in the initial message
            // ex: if syndrome == 00101, then that's 5 in binary, meaning the bit #5 in initial message, including the parity bit,
            //was the error bit
            string log_str;
            char[] syndrome = new char[h1_bits.Count];

            for (int i = 0; i < h1_bits.Count; i++)
            {
                if (h1_bits[i] == h2_bits[i])
                {
                    syndrome[i] = '0';
                }
                else
                {
                    syndrome[i] = '1';
                }
            }

            Array.Reverse(syndrome);
            string syndrome_string = new string(syndrome);
            int pos_flipped_bit = Convert.ToInt32(syndrome_string,2)-1;

            //now flipp that bit back
            if (cArray_full[pos_flipped_bit]=='1')
            {
                cArray_full[pos_flipped_bit] = '0';
            }
            else
            {
                cArray_full[pos_flipped_bit] = '1';
            }

            char[] result = remove_hamming_bits(cArray_full)[0].ToArray();

            log_str = "T= "+ Thread.CurrentThread.Name + "Corrected bit#:" + pos_flipped_bit + Environment.NewLine;
            Logging.log(TypeConsolePrint.Finallog, log_str);

            return result;

        }

        public char[] insert_hamming_codes(string binrep) {
            /*insert hamming codes in binrep, returns a binrep including codes*/
            /*We currently have 272 bits for data frames, and 16 bits for ack/nack. Means I need to somewhat account for that in hamming*/

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

            string log_str = "databef: " + binrep;
            //log_str += Environment.NewLine + "afterHam: " + new string(cArray_total);
            log_str += Environment.NewLine + "hamCodes: " + new string(l.ToArray());

            //string log_str = Environment.NewLine + "hamCodes: " + new string(l.ToArray());
            //Logging.log(TypeConsolePrint.Hamming, log_str);

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
