using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IFT585_TP1
{


    public class Paramètres
    {
        public uint tailleTampon { get; set; }
        public uint delaisTemporisation { get; set; }

        public string emplacementACopier { get; set; }
        public string emplacementCopie { get; set; }
    }

    public class Signal
    {
        //Volatile == used/modified by different threads (potentially). Compiler optimization thing.
        private volatile bool m_isComplete;
        public bool IsComplete
        {
            get { return m_isComplete; }
            set { m_isComplete = value; }
        }
    }

    /*
     * La classe Pipeline implemente un pipelne sequentiel. 
     */
    public class Pipeline
    {

        static Signal g_signal = new Signal();

        private CoucheLLC A1;
        private CoucheLLC B1;
        private CoucheMAC A2;
        private CoucheMAC B2;
        private CouchePhysique C;

        //BlockingCollection = a thread-safe collection. Allows management of concurrent access etc.
        private BlockingCollection<Trame> tramesA1_A2;
        private BlockingCollection<Trame> tramesA2_A1;
        private BlockingCollection<Trame> tramesA_C;
        private BlockingCollection<Trame> tramesC_A;
        private BlockingCollection<Trame> tramesB_C;
        private BlockingCollection<Trame> tramesC_B;
        private BlockingCollection<Trame> tramesB1_B2;
        private BlockingCollection<Trame> tramesB2_B1;

        //Let's create a list of threads to keep track of everything. We append them. Let's also create a list of trame collections
        private List<Thread> all_threads = new List<Thread>();
        private List<BlockingCollection<Trame>> all_trame_collections = new List<BlockingCollection<Trame>>();

        public Pipeline() {}

        public void Run()
        {
            Paramètres param = new Paramètres();

            //TO DO - removes the debug options - see cmts below
            Console.WriteLine("Quelle est la taille du tampon à utiliser de chaque côté du support de transmission?");
            bool entreeCorrecte = false;

            //TO DO: uncommment real code below, comment DEBUG instead
            //#########################DEBUG################
            param.tailleTampon = Convert.ToUInt16(10);
            entreeCorrecte = true;
            //#########################DEBUG################

            //while (!entreeCorrecte)
            //{
            //    try
            //    {
            //        param.tailleTampon = Convert.ToUInt16(Console.ReadLine());
            //        entreeCorrecte = true;
            //    }
            //    catch (InvalidCastException)
            //    {
            //        Console.WriteLine("La valeur entrée n'est pas un entier; svp réessayer :");
            //    }
            //}

            Console.WriteLine("Quel est le délais de temporisation des trames?");
            entreeCorrecte = false;
            //TO DO: uncommment real code below, comment DEBUG instead
            //#########################DEBUG################
            param.delaisTemporisation = Convert.ToUInt16(10);
            entreeCorrecte = true;
            //#########################DEBUG################

            //while (!entreeCorrecte)
            //{
            //    try
            //    {
            //        param.delaisTemporisation = Convert.ToUInt16(Console.ReadLine());
            //        entreeCorrecte = true;
            //    }
            //    catch (InvalidCastException)
            //    {
            //        Console.WriteLine("La valeur entrée n'est pas un entier; svp réessayer :");
            //    }
            //}

            //TO DO: uncommment real code below, comment DEBUG instead
            //#########################DEBUG################
            Console.WriteLine(@"Fichier input: utilise U:\hiver2018\ift585\tp1_liaison\source\t");
            param.emplacementACopier = @"U:\hiver2018\ift585\tp1_liaison\source\t";
            //#########################DEBUG################
            //Console.WriteLine("Quel est l'emplacement du fichier à copier?");
            //param.emplacementACopier = Console.ReadLine();

            //TO DO: uncommment real code below, comment DEBUG instead
            //#########################DEBUG################
            Console.WriteLine(@"Fichier output: utilise U:\hiver2018\ift585\tp1_liaison\source\t");
            param.emplacementCopie = @"U:\hiver2018\ift585\tp1_liaison\dest\t"; ;
            //#########################DEBUG################
            //Console.WriteLine("Quel est l'emplacemenet pour la copie du fichier?");
            //param.emplacementCopie = Console.ReadLine();

            //Easiser to follow stuff than on console
            Logging.createLogFile();

            //I'm not sure we need one collection all the collections?
            tramesA1_A2 = new BlockingCollection<Trame>();
            tramesA2_A1 = new BlockingCollection<Trame>();
            tramesA_C = new BlockingCollection<Trame>();
            tramesC_A = new BlockingCollection<Trame>();
            tramesB_C = new BlockingCollection<Trame>();
            tramesC_B = new BlockingCollection<Trame>();
            tramesB1_B2 = new BlockingCollection<Trame>();
            tramesB2_B1 = new BlockingCollection<Trame>();


            all_trame_collections.Add(tramesA1_A2);
            all_trame_collections.Add(tramesA2_A1);
            all_trame_collections.Add(tramesA_C);
            all_trame_collections.Add(tramesC_A);
            all_trame_collections.Add(tramesB_C);
            all_trame_collections.Add(tramesC_B);
            all_trame_collections.Add(tramesB1_B2);
            all_trame_collections.Add(tramesB2_B1);



            A1 = new CoucheLLC(g_signal);
            A2 = new CoucheMAC(g_signal, A1);
            B1 = new CoucheLLC(g_signal);
            B2 = new CoucheMAC(g_signal, B1);
            C = new CouchePhysique(g_signal, A2, B2);

            /*init A1, B1. This creates their CoucheReseau essentially. Let's suppose A1 sends (is_receiving=false) and B1 receives...*/
            A1.Initialiser(param.emplacementACopier, is_active:false);
            B1.Initialiser(param.emplacementCopie, is_active:true);
            Thread threadA1 = new Thread(A1.Run);
            Thread threadA2 = new Thread(A2.Run);
            Thread threadB1 = new Thread(B1.Run);
            Thread threadB2 = new Thread(B2.Run);
            Thread threadC = new Thread(C.Run);

            //easier to debug
            threadA1.Name = "A1_LLC";
            threadA2.Name = "A2_MAC";
            threadB1.Name = "B1_LLC";
            threadB2.Name = "B2_MAC";
            threadC.Name = "C_PHYS";


            all_threads.Add(threadA1);
            all_threads.Add(threadA2);
            all_threads.Add(threadB1);
            all_threads.Add(threadB2);
            all_threads.Add(threadC);

            try
            {

                /*MS documentation directly: Causes the operating system to change the state of 
                the current instance to ThreadState.Running, and optionally supplies an object 
                containing data to be used by the method the thread executes.*/

                //Apparently you can pass arguments to the threads through Start()
                threadA1.Start();
                threadA2.Start();
                threadB1.Start();
                threadB2.Start();
                threadC.Start();
            }
            finally
            {
                threadA1.Join();
                threadA2.Join();
                threadB1.Join();
                threadB2.Join();
                threadC.Join();
            }

        }

        public void Reset()
        {
            if (g_signal.IsComplete != false)
            {
                g_signal.IsComplete = false;
            }
        }
    }
}
