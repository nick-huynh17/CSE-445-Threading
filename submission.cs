using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

/**
 * This template file is created for ASU CSE445 Distributed SW Dev Assignment 2.
 * Please do not modify or delete any existing class/variable/method names. However, you can add more variables and functions.
 * Uploading this file directly will not pass the autograder's compilation check, resulting in a grade of 0.
 * **/

namespace ConsoleApp1
{
    //delegate declaration for creating events
    public delegate void PriceCutEvent(double roomPrice, Thread agentThread);
    public delegate void OrderProcessEvent(Order order, double orderAmount);
    public delegate void OrderCreationEvent();

    public class MainClass
    {
        public static MultiCellBuffer buffer;
        public static Thread[] travelAgentThreads;
        public static bool hotelThreadRunning = true;
        public static void Main(string[] args)
        {
            
            Console.WriteLine("Inside Main");
            buffer = new MultiCellBuffer();

            Hotel hotel = new Hotel();
            TravelAgent travelAgent = new TravelAgent();

            Thread hotelThread = new Thread(new ThreadStart(hotel.hotelFun));
            hotelThread.Start();

            Hotel.PriceCut += new PriceCutEvent(travelAgent.agentOrder);
            Console.WriteLine("Price cut event has been subscribed");
            TravelAgent.orderCreation += new OrderCreationEvent(hotel.takeOrder);
            Console.WriteLine("Order creation event has been subscribed");
            OrderProcessing.OrderProcess += new OrderProcessEvent(travelAgent.orderProcessConfirm);
            Console.WriteLine("Order process event has been subscribed");

            travelAgentThreads = new Thread[5];
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine("Creating  travel agent thread {0}", (i + 1));
                travelAgentThreads[i] = new Thread(travelAgent.agentFun);
                travelAgentThreads[i].Name = (i + 1).ToString();
                travelAgentThreads[i].Start();
            }
        }
    }
    public class MultiCellBuffer
    {
        // Each cell can contain an order object
        private const int bufferSize = 3; //buffer size
        int usedCells;
        private Order[] multiCells; // ? mark make the type nullable: allow to assign null value
        public static Semaphore getSemaph;
        public static Semaphore setSemaph;
        private readonly object lockObj = new object();

        public MultiCellBuffer() //constructor 
        {   // Creates three empty slots
            multiCells = new Order[bufferSize];
            setSemaph = new Semaphore(bufferSize, bufferSize); // All slots are free
            getSemaph = new Semaphore(0, bufferSize); // None are filled
        }

        public void SetOneCell(Order data)
        {
            if (!setSemaph.WaitOne(0)) // If there isn't a free cell, return
            {
                return;
            }

            lock (lockObj)

            {   // Find free cell to store data
                for (int i = 0; i < multiCells.Length; i++) 
                {
                    if (multiCells[i] == null)
                    {
                        multiCells[i] = data;
                        break;
                    }
                }
            }
            getSemaph.Release(); // Record that a cell has been filled
        }

        public Order GetOneCell()
        {
            if (!getSemaph.WaitOne(0)) // If there isn't a filled cell, return null
            {
                return null;
            }

            Order order = null;

            lock (lockObj)
            {   // Find a filled cell to clear
                for (int i = 0; i < multiCells.Length; i++)
                {
                    if (multiCells[i] != null)
                    {
                        order = multiCells[i];
                        multiCells[i] = null;
                        break;
                    }
                }
            }
            setSemaph.Release(); // Record that a cell is freed
            return order;
        }
    }
    public class Order
    {
        //identity of sender of order
        private string senderId;
        //credit card number
        private long cardNo;
        //unit price of room from hotel
        private double unitPrice;
        //quantity of rooms to order
        private int quantity;

        //parametrized constructor
        public Order(string senderId, long cardNo, double unitPrice, int quantity)
        {   // Sets the local values to updated ones
            this.senderId = senderId;
            this.cardNo = cardNo;
            this.unitPrice = unitPrice;
            this.quantity = quantity;
        }

        //getter methods
        public string getSenderId()
        {
            return senderId;
        }

        public long getCardNo()
        {
            return cardNo;
        }
        public double getUnitPrice()
        {
            return unitPrice;
        }
        public int getQuantity()
        {
            return quantity;
        }

    }

    public class OrderProcessing
    {
        public static event OrderProcessEvent OrderProcess;
        static Random rnd = new Random();
        //method to check for valid credit card number input
        public static bool creditCardCheck(long creditCardNumber)
        {   // Only accept credit card #'s between 5000-7000
            if (creditCardNumber >= 5000 &&  creditCardNumber <= 7000)
            {
                return true;
            }
            return false;
        }

        //method to calculate the final charge after adding taxes, location charges, etc
        public static double calculateCharge(double unitPrice, int quantity)
        {   // Calculate the values by using given values and randomizing the tax and location charges
            double quantityPrice = quantity * unitPrice;
            double tax = rnd.NextDouble() * (0.12 - 0.08) + 0.08;
            double location = rnd.NextDouble() * (80 - 20) + 20;
            double final = quantityPrice * (1 + tax) + location;
            return final;
        }

        //method to process the order
        public static void ProcessOrder(Order order)
        {
            if (order == null)
            {
                return;
            }
            if (!creditCardCheck(order.getCardNo())) // If card number is invalid callback with a zero
            {
                OrderProcess?.Invoke(order, 0);
                return;
            }
            // Compute bill for the order and assign it
            double total = calculateCharge(order.getUnitPrice(), order.getQuantity()); 
            OrderProcess?.Invoke(order, total);
        }
    }

    public class TravelAgent
    {
        public static event OrderCreationEvent orderCreation;
        static Random rnd = new Random();
        private double priceCut;

        public void agentFun()
        {   // Keeps the agent thread going while hotel is being used
            while (MainClass.hotelThreadRunning)
            {
                Thread.Sleep(500);
            }
        }
        public void orderProcessConfirm(Order order, double orderAmount)
        {
            Console.WriteLine( "Travel Agent {0}'s order is confirmed. The price is {1:F2}", order.getSenderId(), orderAmount); // Prints out the order and pricing
        }

        private void createOrder(string senderId)
        {   // Creates a order with randomly generated quantity of rooms and credit card number and assigns it to the senderID to make an order
            int rooms = rnd.Next(1, 11);
            long creditCardNumber = rnd.Next(5000, 7001);
            Order order = new Order(senderId, creditCardNumber, priceCut, rooms);
            MainClass.buffer.SetOneCell(order);
            orderCreation();
        }
        public void agentOrder(double roomPrice, Thread travelAgent) // Callback from hotel thread
        {   // Updates the price and creates an order for the agent
            priceCut = roomPrice;
            string senderID = travelAgent.Name;
            createOrder(senderID);
        }
    }

    public class Hotel
    {
        static double currentRoomPrice = 100; //random current agent price
        static int threadNo = 0;
        static int eventCount = 0;
        static Random rnd = new Random();
        public static event PriceCutEvent PriceCut;

        public void hotelFun()
        {
            while (eventCount < 10 && MainClass.hotelThreadRunning)
            {   // Randomizes and updates the pricing as long as it hasn't gone down 10 times and the thread is still running
                double pricing = pricingModel();
                updatePrice(pricing);
            }
            MainClass.hotelThreadRunning = false;
        }
        //using random method to generate random room prices
        public double pricingModel()
        {   // Randomizes the ups and downs in the pricing between the values of 80 and 160 but never over or under that range
            double change = (rnd.NextDouble() - .5) * 25;
            double price = currentRoomPrice + change;
            if (price <80)
            {
                price = 80;
            }
            if (price > 160) {
                price = 160;
            }
            return price;
        }

        public void updatePrice(double newRoomPrice)
        {   // Increments the events if there was a price cut then updates value for each of the agents in the agents thread
            if (newRoomPrice < currentRoomPrice)
            {
                currentRoomPrice = newRoomPrice;
                eventCount++;

                foreach (Thread t in MainClass.travelAgentThreads)
                {
                    PriceCut(newRoomPrice, t);
                }
            }
            else
            {
                currentRoomPrice = newRoomPrice;
            }
        }

        public void takeOrder() // callback from travel agent
        {   // Takes an order from an agent as long as its not an empty order
            Order order = MainClass.buffer.GetOneCell();
            if (order == null)
            {
                return;
            }

            Thread processOrder = new Thread(() => OrderProcessing.ProcessOrder(order));
            processOrder.Start();
        }
    }
}
