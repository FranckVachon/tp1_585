using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace IFT585_TP1
{
    // TO DO :
    //  -  Gestion du cheksum pour la levée de l'évènement CkSumErr
    public class CoucheLLC
    {
        /*
         * Classe CoucheReseau
         * 
         * Prend en charge la lecture et l'écriture d'un fichier.
         * Envoie, sous forme de paquets, l'information à la sous-couche LLC.
         * 
         * TO DO :
         *  - Écrire fichier à partir du flux m_paquetsIn;
         */
        private class CoucheReseau
        {
            private Thread m_thread;
            private string m_path;
            private BlockingCollection<Paquet> m_paquetsIn;
            private BlockingCollection<Paquet> m_paquetsOut;
            private BlockingCollection<TypeEvenement> m_evenementStream;

            private volatile bool m_estActive;
            public bool EstActive
            {
                get { return m_estActive; }
                set { m_estActive = value; }
            }

            public CoucheReseau(string path, BlockingCollection<Paquet> paquetsOut, BlockingCollection<Paquet> paquetsIn, BlockingCollection<TypeEvenement> evenements, bool isReceiving)
            {
                this.m_evenementStream = evenements;
                //Paquets_in? How do we know at the instantiation
                this.m_paquetsIn = paquetsIn;
                this.m_paquetsOut = paquetsOut;     //input file read from disk goes here, e.g. paquet "waiting to go out"
                this.m_path = path;
                this.m_estActive = false;
                m_thread = isReceiving ? null : new Thread(_lireFichier);
            }

            public void Partir()
            {
                if (this.m_thread != null)
                    this.m_thread.Start();
            }

            public void Terminer()
            {
                if (this.m_thread != null)
                    this.m_thread.Join();
            }

            public void ecrire_paquet()
            {
                /*Not ideal - Rez should probably be a class of its own?
                Because it needs a run() to check for new stuff, no?*/
                _ecrireFichier();
            }

            private void _lireFichier()
            {

                using (FileStream fs = File.OpenRead(m_path))
                {
                    int nbOctetsALire = (int)fs.Length;
             
                    while (nbOctetsALire > 0)
                    {
                        if (!m_estActive)
                        {
                            Thread.Sleep(500);
                            continue;
                        }
                        else
                        {
                            Paquet paquet = new Paquet();

                            int read = fs.Read(paquet.Buffer, 0, (int)paquet.Taille);

                            m_paquetsOut.Add(paquet);
                            //Adding events - we have a file to send (e.g. CoucheReseauPrete)
                            //What happens after I add CCoucheReseauPrete to the event stream? Who "knows" about that?
                            m_evenementStream.Add(TypeEvenement.CoucheReseauPrete);

                            if (read <= 0)
                                break;

                            nbOctetsALire -= read;
                        }
                    }
                }

                //log stuff
                string log_str = "_lireFichier T=" + Thread.CurrentThread.Name + " content from file: " + Environment.NewLine;
                foreach (Paquet p in m_paquetsOut)
                {
                    log_str += p.ToString() +Environment.NewLine;
                }

                Logging.log(TypeConsolePrint.SendingPath, log_str);

                Terminer();
            }
            
            private void _ecrireFichier() {

                try
                {
                    //Paquet p = m_paquetsIn.Take();
                    //File.WriteAllBytes(m_path, p.Buffer);

                    //log stuff
                    string log_str = "_ecrireFichier T=" + Thread.CurrentThread.Name + " content to write: " + Environment.NewLine;
                    foreach (Paquet pA in m_paquetsIn)
                    {
                        log_str += pA.ToString() + Environment.NewLine;
                    }

                    Logging.log(TypeConsolePrint.ReceptionPath, log_str);
                    Paquet p = m_paquetsIn.Take();
                    using (var fs = new FileStream(m_path, FileMode.Append, FileAccess.Write))
                    {
                        fs.Write(p.Buffer,0,Convert.ToInt32(p.Taille));
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception _ecrireFichier from Thread.Name {0} for Paquet {1},", Thread.CurrentThread.Name, m_paquetsIn.ToString());
                    Console.WriteLine("except {0}", ex);
                    throw ex;
                }



            }
        }

        private class Chrono
        {
            private class MyTimer : System.Timers.Timer
            {
                public MyTimer(uint ID, Chrono parent) {
                    this.ID = ID;
                    this.Parent = parent;
                }

                public uint ID { get; set; }   
                public Chrono Parent { get; set; }
            }

            private MyTimer[] m_timers;

            private BlockingCollection<TypeEvenement> m_eventStream;
            public BlockingCollection<TypeEvenement> EventStream
            {
                get { return m_eventStream; }
            }

            private BlockingCollection<uint> m_plusVieilleTrameStream;
            public BlockingCollection<uint> PlusVieilleTrameStream
            {
                get { return m_plusVieilleTrameStream; }
            }

            public Chrono(BlockingCollection<TypeEvenement> eventStream)
            {
                this.m_eventStream = eventStream;
                this.m_timers = new MyTimer[Constants.NB_BUFS];
                uint cpt = 0;
                //TO DO: check this
                for (int i = 0; i < Constants.NB_BUFS; i++)
                {
                    MyTimer t = new MyTimer(cpt++, this);
                    t.Elapsed += OnTimedEvent;
                    t.Interval = (double)Constants.TIMEOUT;
                    m_timers[i] = t;
                }

                this.m_plusVieilleTrameStream = new BlockingCollection<uint>();
            }

            public void PartirChrono(uint noTrame)
            {
                uint fenetre = noTrame % Constants.NB_BUFS;
                this.m_timers[fenetre].ID = noTrame;
                this.m_timers[fenetre].Start();
            }

            public void StopChrono(uint fenetre)
            {
                this.m_timers[fenetre].Stop();
            }

            private void Detruire()
            {
                foreach (MyTimer chrono in this.m_timers)
                {
                    chrono.Dispose();
                }
            }

            private static void OnTimedEvent(Object source, ElapsedEventArgs e)
            {
                MyTimer chrono = source as MyTimer;
                if (chrono != null)
                {
                    chrono.Parent.PlusVieilleTrameStream.Add(chrono.ID);
                    chrono.Parent.EventStream.Add(TypeEvenement.Timeout);
                }
            }
        }

        private class ACKTimer : System.Timers.Timer
        {
            private bool m_estArme;
            public bool EstArme
            {
                set { m_estArme = value; }
            }

            private BlockingCollection<TypeEvenement> m_eventStream;
            public BlockingCollection<TypeEvenement> EventStream
            {
                get { return m_eventStream; }
            }

            public ACKTimer(BlockingCollection<TypeEvenement> eventStream)
            {
                this.m_eventStream = eventStream;
                this.m_estArme = false;

                this.Interval = (double)Constants.ACK_TIMEOUT;
                // Hook up the Elapsed event for the timer. 
                this.Elapsed += OnTimedEvent;
            }

            public void StartACKTimer()
            {
                if (!m_estArme)
                {
                    this.Start();
                    m_estArme = true;
                }
            }

            public void StopACKTimer()
            {
                if (m_estArme)
                {
                    this.Stop();
                    m_estArme = false;
                }
            }

            private static void OnTimedEvent(Object source, ElapsedEventArgs e)
            {
                ACKTimer chrono = source as ACKTimer;
                if (chrono != null)
                {
                    chrono.EventStream.Add(TypeEvenement.ACKTimeout);
                    chrono.EstArme = false;
                }
            }
        }

        static bool noNAK = true;      /* Pas encore reçu d'aquitement négatif*/

        private BlockingCollection<TypeEvenement> m_eventStream;
        public BlockingCollection<TypeEvenement> EvenementStream 
        {
            get { return m_eventStream; }
        }

        private BlockingCollection<Trame> m_MACStreamIn;
        public BlockingCollection<Trame> MACStreamIn
        {
            get { return m_MACStreamIn; }
        }

        private BlockingCollection<Trame> m_MACStreamOut;
        public BlockingCollection<Trame> MACStreamOut
        {
            get { return m_MACStreamOut; }
        }

        private BlockingCollection<Paquet> m_reseauStreamIn;
        private BlockingCollection<Paquet> m_reseauStreamOut;

        private CoucheReseau m_coucheReseau;
        private Signal m_signal;
        private Chrono m_chrono;
        private ACKTimer m_ackTimer;

        public CoucheLLC(Signal signal)
        {
            this.m_signal = signal;

            this.m_MACStreamIn = new BlockingCollection<Trame>();
            this.m_MACStreamOut = new BlockingCollection<Trame>();

            this.m_eventStream = new BlockingCollection<TypeEvenement>();
            this.m_reseauStreamIn = new BlockingCollection<Paquet>();
            this.m_reseauStreamOut = new BlockingCollection<Paquet>();

            this.m_chrono = new Chrono(m_eventStream);
            this.m_ackTimer = new ACKTimer(m_eventStream);
        }

        //Not convinced this should be 2 different method - it should be Initial period. We can use the _activerCouche or an additional Param in the init...
        public void Initialiser(string path, Boolean is_active=false)
        {
            this.m_coucheReseau = new CoucheReseau(path, m_reseauStreamIn, m_reseauStreamOut, m_eventStream, is_active);
        }

        private void _activerCoucheReseau() 
        {
            this.m_coucheReseau.EstActive = true;
        }

        private void _desactiverCoucheReseau()
        {
            this.m_coucheReseau.EstActive = false;
        }

        private void _origineCoucheReseau(out Paquet paquet)
        {
            //We do have bytes in paquet after this
            paquet = this.m_reseauStreamIn.Take();

            string log_str = "_versCoucheReseau from Thread.Name: " + Thread.CurrentThread.Name + " paquets to write: " + paquet.ToString();
            Logging.log(TypeConsolePrint.SendingPath, log_str);

        }

        private void _origineCouchePhysique(out Trame trame)
        {
            trame = this.m_MACStreamIn.Take();
            string log_str = "_origineCouchePhysique from Thread.Name: " + Thread.CurrentThread.Name + " for noTrame: " + trame.ToString();
            Logging.log(TypeConsolePrint.ReceptionPath, log_str);
        }

        private void _versCoucheReseau(Paquet paquet)
        {
            string log_str = "_versCoucheReseau from Thread.Name: " + Thread.CurrentThread.Name + " paquets to write: " + paquet.ToString();
            Logging.log(TypeConsolePrint.ReceptionPath, log_str);

            m_reseauStreamOut.Add(paquet);
            m_eventStream.Add(TypeEvenement.PaquetRecu);

        }

        private void _versCouchePhysique(Trame trame) 
        {

            string log_str = "_versCouchePhysique from Thread.Name: " + Thread.CurrentThread.Name + " for noTrame: " + trame.ToString();
            Logging.log(TypeConsolePrint.SendingPath, log_str);

            m_MACStreamOut.Add(trame);
        }

        /*
         * Retourne 'true' si (a <= b < c) de manière circulaire; 'false' autrement.
         */
        static bool EstAuMillieu(uint a, uint b, uint c)
        {
            return ((a <= b) && (b < c)) || ((c < a) && (a <= b)) || ((b < c) && (c < a));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeTrame"></param>
        /// <param name="noTrame"></param>/
        /// <param name="noTrameAttendue"></param>
        /// <param name="tampon"></param>
        /* 
         * Construction et envoie d'une trame de données ou d'une trame ACK ou NAK 
         */
        private void _envoyerTrame(TypeTrame typeTrame, uint noTrame, uint noTrameAttendue, Paquet[] tampon)
        {
            /*Preparation de la trame a envoyer*/

            Trame s = new Trame();    /* variable scratch */

            /* Préparer la trame */
            s.Type = typeTrame;

            if (typeTrame == TypeTrame.data)
                s.Info = tampon[noTrame % Constants.NB_BUFS];

            s.NoSequence = noTrame;
            //This yeild s.ACK = 7. Why ACK==7? 
            //Should ACK be carried with the trame? I would rather keep track of trames
            //ACKed and not ACKed in LLC, not in the trame itself?
            s.ACK = (noTrameAttendue + Constants.MAX_SEQ) % (Constants.MAX_SEQ + 1);

            if (typeTrame == TypeTrame.nak)
                noNAK = false;  /* Un seul NAK par trame svp */

            /* Transmission de la trame */
            _versCouchePhysique(s);

            if (typeTrame == TypeTrame.data)
                /* Armement du Timer */
                this.m_chrono.PartirChrono(noTrame);

            this.m_ackTimer.StopACKTimer();

            //Logging
            string log_str = "_envoyerTrame from Thread.Name: " + Thread.CurrentThread.Name + " for noTrame: " + s.ToString();
            Logging.log(TypeConsolePrint.SendingPath, log_str);
        }

        private TypeEvenement _attendreEvenement()
        {
            TypeEvenement evenement = m_eventStream.Take();

            string log_str = "evenement from Thread.Name: " + Thread.CurrentThread.Name + " for eventname: " + evenement.ToString();
            Logging.log(TypeConsolePrint.Event, log_str);

            return evenement;
        }

        public void Run()
        {
            uint ackAttendu;                /* Bord inf. fenêtre émetteur */
            uint prochaineTramePourEnvoie;  /* Bord sup. fenêtre émetteur + 1 */
            uint trameAttendue;             /* Bord inf. fenêtre récepteur */
            uint tropLoin;                  /* Bord sup. fenêtre récepteur + 1 */
            uint index;                     /* Index d'accès au tampon */

            Trame temp_trame = new Trame();          /* Variable temporaire */
            Paquet[] outTampon = new Paquet[Constants.NB_BUFS];   /* Tampon pour le flux de données en sortie */
            Paquet[] inTampon = new Paquet[Constants.NB_BUFS];   /* Tampon pour le flux de données en entrée */
            bool[] estArrive = new bool[Constants.NB_BUFS]; /* Tampon occupé ou non */
            uint nbTampons;                 /* Nombre de tampons sortie en cours d'utilisation */
            TypeEvenement evenement;

            /* Initialisation */
            _activerCoucheReseau();
            this.m_coucheReseau.Partir();

            ackAttendu = 0;
            prochaineTramePourEnvoie = 0;
            trameAttendue = 0;
            tropLoin = Constants.NB_BUFS;

            /* Au départ, pas de paquets en mémoire */
            nbTampons = 0;
            for (index = 0; index < Constants.NB_BUFS; index++) estArrive[index] = false;

            //TO DO: is this signal ever turned off? Maybe this is why I get multiple times the same file at the end?
            while (!this.m_signal.IsComplete)
            {
                evenement = _attendreEvenement();

                switch (evenement)
                {
                    case TypeEvenement.CoucheReseauPrete:  /* Accepter et transmettre la nouvelle trame */
                        /* Agrandit la fenêtre */
                        ++nbTampons;
                        /* Acquisition. Out means we pass by reference, not by value */
                        _origineCoucheReseau(out outTampon[prochaineTramePourEnvoie % Constants.NB_BUFS]);
                        /* Transmission */
                        _envoyerTrame(TypeTrame.data, prochaineTramePourEnvoie, trameAttendue, outTampon);
                        /* Avance bord fenêtre */
                        ++prochaineTramePourEnvoie;
                        break;
                    case TypeEvenement.ArriveeTrame:       /* Arrivé d'une trame de données ou de contrôle */
                        /* Acquisition - _origineCouchePhysique returns the trame it received */
                        _origineCouchePhysique(out temp_trame);

                        if (temp_trame.Type == TypeTrame.data)
                        {
                            /* C'est une trame de données correctes */
                            //?How do we know it's good? Check has been called where?
                            if ((temp_trame.NoSequence != trameAttendue) && noNAK)
                                _envoyerTrame(TypeTrame.nak, 0, trameAttendue, outTampon);
                            else
                                this.m_ackTimer.StartACKTimer();

                            if (EstAuMillieu(trameAttendue, temp_trame.NoSequence, tropLoin) && (estArrive[temp_trame.NoSequence % Constants.NB_BUFS] == false))
                            {
                                /* On doit accepter les trames dans n'importe quel ordre */

                                /* Tampon remplis avec les données */
                                estArrive[temp_trame.NoSequence % Constants.NB_BUFS] = true;
                                inTampon[temp_trame.NoSequence % Constants.NB_BUFS] = temp_trame.Info;

                                while (estArrive[trameAttendue % Constants.NB_BUFS])
                                {
                                    /* Passage trames et avancée fenêtre */
                                    _versCoucheReseau(inTampon[trameAttendue % Constants.NB_BUFS]);
                                    noNAK = true;
                                    estArrive[trameAttendue % Constants.NB_BUFS] = false;

                                    ++trameAttendue;     /* Avance bord inf. fenêtre récepteur */
                                    ++tropLoin;          /* Avance bord haut fenêtre récepteur */
                                    this.m_ackTimer.StartACKTimer();
                                }
                            }


                            //TO DO - seems we don't do anything with it
                        }

                        if ((temp_trame.Type == TypeTrame.nak) && EstAuMillieu(ackAttendu, (temp_trame.ACK + 1) % (Constants.MAX_SEQ + 1), prochaineTramePourEnvoie))
                            _envoyerTrame(TypeTrame.data, (temp_trame.ACK + 1) % (Constants.MAX_SEQ + 1), trameAttendue, outTampon);

                        while (EstAuMillieu(ackAttendu, temp_trame.ACK, prochaineTramePourEnvoie))
                        {
                            /* Traitement ACK superposé */
                            --nbTampons;
                            /* Trame arrivée intacte */
                            this.m_chrono.StopChrono(ackAttendu % Constants.NB_BUFS);
                            /* Avance bord bas fenêtre émetteur */
                            ++ackAttendu;
                        }
                        break;
                    case TypeEvenement.CkSumErr:
                        if (noNAK)
                            _envoyerTrame(TypeTrame.nak, 0, trameAttendue, outTampon);   /* Trame altérée */
                        break;
                    case TypeEvenement.Timeout:
                        _envoyerTrame(TypeTrame.data, this.m_chrono.PlusVieilleTrameStream.Take(), trameAttendue, outTampon);
                        break;
                    case TypeEvenement.ACKTimeout:
                        /* Timer ACK expiré => Enovie ACK */
                        _envoyerTrame(TypeTrame.ack, 0, trameAttendue, outTampon);
                        break;

                    case TypeEvenement.PaquetRecu:
                        /* Have a package - send that to Couche Reseau */
                        m_coucheReseau.ecrire_paquet();
                        break;
                        
                }

                if (nbTampons < Constants.NB_BUFS)
                    _activerCoucheReseau();
                else
                    _desactiverCoucheReseau();
            }
        }
    }
}