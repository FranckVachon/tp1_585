using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace IFT585_TP1
{
    public class CoucheMAC
    {
        private BlockingCollection<Trame> m_physiqueStreamIn;
        public BlockingCollection<Trame> PhysiqueStreamIn
        {
            get { return m_physiqueStreamIn; }
        }

        private BlockingCollection<Trame> m_physiqueStreamOut;
        public BlockingCollection<Trame> PhysiqueStreamOut
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

            this.m_physiqueStreamIn = new BlockingCollection<Trame>();
            this.m_physiqueStreamOut = new BlockingCollection<Trame>();
        }

        public void envoie_trame(Trame completeFrame) {

            string log_str = "envoie_trame T= " + Thread.CurrentThread.Name + " noTrame: " + completeFrame.NoSequence;
            Logging.log(TypeConsolePrint.SendingPath, log_str);



            log_str = "Trame avan binrep: " + completeFrame.ToString() + Environment.NewLine;
            Logging.log(TypeConsolePrint.MACDEBUG, log_str);

            //First, need to take the bytes[] from that frame and turn them into a series of 0101010101 strings we will be hamming on
            string binrep = bytes_to_bin_string(completeFrame);

            Trame tr = binString_to_trame(binrep);
            log_str = "Trame APRES binrep/reconvert: " + tr.ToString() + Environment.NewLine;
            Logging.log(TypeConsolePrint.MACDEBUG, log_str);
            //Then, feed that to hamming() to insert the control bits

            m_physiqueStreamOut.Add(completeFrame);
        }

        public void reception_trame(Trame completeFrame)
        {

            string log_str = "reception_trame from Thread.Name: " + Thread.CurrentThread.Name + " noTrame: " + completeFrame.NoSequence;
            Logging.log(TypeConsolePrint.ReceptionPath, log_str);
            m_LLCStreamOut.Add(completeFrame);
            m_evenementStream.Add(TypeEvenement.ArriveeTrame);
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

            //TO DO: add constructor to Trame(args)
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


                if (m_physiqueStreamIn.TryTake(out completeFrame, 100))
                {
                    /* Trame provenant de la couche physique */

                    // TO DO : Faire le traitement de la sous-couche MAC
                    reception_trame(completeFrame);

                }
            }
        }
    }
}
