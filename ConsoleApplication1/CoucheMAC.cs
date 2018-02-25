using System;
using System.Collections.Concurrent;
using System.Threading;

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

            string log_str = "envoie_trame from Thread.Name: " + Thread.CurrentThread.Name + " noTrame: " + completeFrame.NoSequence;
            Logging.log(TypeConsolePrint.SendingPath, log_str);
            m_physiqueStreamOut.Add(completeFrame);
        }

        public void reception_trame(Trame completeFrame)
        {

            string log_str = "reception_trame from Thread.Name: " + Thread.CurrentThread.Name + " noTrame: " + completeFrame.NoSequence;
            Logging.log(TypeConsolePrint.ReceptionPath, log_str);
            m_LLCStreamOut.Add(completeFrame);
            m_evenementStream.Add(TypeEvenement.ArriveeTrame);
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
