using System;
using System.Threading;
using System.Collections.Concurrent;

namespace IFT585_TP1
{
    public class CouchePhysique
    {
        /* //Actually want to have strings, not trames, going through the physical layer. Just commenting for now but will eventually delete
        private BlockingCollection<Trame> m_A2StreamIn;
        private BlockingCollection<Trame> m_A2StreamOut;
        private BlockingCollection<Trame> m_B2StreamIn;
        private BlockingCollection<Trame> m_B2StreamOut;
        */

        private BlockingCollection<char[]> m_A2StreamIn;
        private BlockingCollection<char[]> m_A2StreamOut;
        private BlockingCollection<char[]> m_B2StreamIn;
        private BlockingCollection<char[]> m_B2StreamOut;
        public CouchePhysique(Signal signal, CoucheMAC A2, CoucheMAC B2)
        {
            m_A2StreamIn = A2.PhysiqueStreamOut;
            m_A2StreamOut = A2.PhysiqueStreamIn;
            m_B2StreamIn = B2.PhysiqueStreamOut;
            m_B2StreamOut = B2.PhysiqueStreamIn;
        }

        public void Run()
        {
            while (true) 
            {

                char[] dummy = null;        //because we can't do  var cArray; or var cArray = null;
                var cArray = dummy;

                if (m_A2StreamIn.TryTake(out cArray, 100))
                {
                    /* Trame provenant de A */

                    // TO DO : Faire les perturbations de la couche physique

                    m_B2StreamOut.Add(cArray);
                    //Logging
                    //string log_str = "streamout from T=" + Thread.CurrentThread.Name + " for frame: " + completeFrame.ToString();
                    //Logging.log(TypeConsolePrint.SendingPath, log_str);
                }


                if (m_B2StreamIn.TryTake(out cArray, 100))
                {
                    /* Trame provenant de B */

                    // TO DO : Faire les perturbations de la couche physique

                    m_A2StreamOut.Add(cArray);
                    //Logging
                    //string log_str = "streamout from T=" + Thread.CurrentThread.Name + " for frame: " + completeFrame.ToString();
                    //Logging.log(TypeConsolePrint.SendingPath, log_str);
                }
            }
        }
    }
}
