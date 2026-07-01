using System;
using System.Collections.Generic;
using System.Linq;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ErpDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Customers.Any())
            {
                return; // DB has been seeded
            }

            // 1. Seed Agents
            var agents = new List<Agent>
            {
                new() { Name = "N.JAYAPRAKASH", Email = "jayaprakash@ariyaitech.com", Phone = "9876543210" },
                new() { Name = "GM MARKETING", Email = "gm.marketing@ariyaitech.com", Phone = "9876543211" },
                new() { Name = "U. THALAIMALAI", Email = "thalaimalai@ariyaitech.com", Phone = "9876543212" },
                new() { Name = "STS MARKETING", Email = "sts.marketing@ariyaitech.com", Phone = "9876543213" },
                new() { Name = "AJITH", Email = "ajith@ariyaitech.com", Phone = "9876543214" },
                new() { Name = "K. NAGANATHAN", Email = "naganathan@ariyaitech.com", Phone = "9876543215" },
                new() { Name = "YESPEE ASSOCIATES", Email = "yespee@ariyaitech.com", Phone = "9876543216" }
            };
            context.Agents.AddRange(agents);
            context.SaveChanges();

            // 2. Seed Customers
            var customers = new List<Customer>
            {
                new() { Name = "Sri Manjunatha Spinning Mills Ltd", Address = "Door No. 12, Industrial Area", City = "Guntur", State = "Andhra Pradesh", Country = "India" },
                new() { Name = "Pasupati Spinning & Weaving Mills Ltd (Kala Unit)", Address = "Kala Amb, Sahanpur Road", City = "Sirmaur", State = "Himachal Pradesh", Country = "India" },
                new() { Name = "Pongalur Pioneer Textiles Pvt Ltd.", Address = "Avinashi Road, Pongalur", City = "Tirupur", State = "Tamil Nadu", Country = "India" },
                new() { Name = "S.P.APPARELS LTD (SPINNING DIVISION - II)", Address = "S.F No. 234, Salem Bypass", City = "Salem", State = "Tamil Nadu", Country = "India" },
                new() { Name = "SUMANLAL J. SHAH & CO(sales)", Address = "A-34, Textile Market, OPP Station", City = "Coimbatore", State = "Tamil Nadu", Country = "India" },
                new() { Name = "PERIYANAYAKKI COTTON MILL,", Address = "11-A, Mill Road", City = "Coimbatore", State = "Tamil Nadu", Country = "India" },
                new() { Name = "PARTH THREAD (P) LTD.,", Address = "Plot 92, Industrial Focal Point", City = "Ambedkar Nagar", State = "Uttar Pradesh", Country = "India" },
                new() { Name = "Precot Limited - \"A\" Unit,", Address = "Kanjikode West", City = "Palakkad", State = "Kerala", Country = "India" },
                new() { Name = "TRISHUL OVERSEAS", Address = "24, Netaji Subhash Marg", City = "Delhi", State = "Delhi", Country = "India" },
                new() { Name = "Yarncoms India Pvt Ltd.,", Address = "15/3, Kamaraj Road", City = "Coimbatore", State = "Tamil Nadu", Country = "India" },
                new() { Name = "A.K ENTERPRISES (SALES),", Address = "Flat 12, Sector 5", City = "Delhi", State = "Delhi", Country = "India" },
                new() { Name = "JOHN MILTON A", Address = "Block C, Gandhi Road", City = "Chennai", State = "Tamil Nadu", Country = "India" },
                new() { Name = "4S Spintex India Pvt Ltd", Address = "Plot 89, SIPCOT", City = "Nellore", State = "Andhra Pradesh", Country = "India" },
                new() { Name = "SANJAY AGENCIES", Address = "54, Raja Street", City = "Madurai", State = "Tamil Nadu", Country = "India" },
                new() { Name = "A.P.SPINNERS", Address = "102, Mill Colony", City = "Coimbatore", State = "Tamil Nadu", Country = "India" },
                new() { Name = "A.C. TEXTILE MILLS", Address = "2/123, Main Road", City = "Salem", State = "Tamil Nadu", Country = "India" },
                new() { Name = "ANGALAJOTHI SPINNERS,", Address = "Palladam Road", City = "Tirupur", State = "Tamil Nadu", Country = "India" },
                new() { Name = "HARSHNI TEXTILES PVT LTD", Address = "Sulur Road, Trichy Highway", City = "Coimbatore", State = "Tamil Nadu", Country = "India" }
            };
            context.Customers.AddRange(customers);
            context.SaveChanges();

            // 3. Seed Products
            var products = new List<Product>
            {
                new() { Group = "Yarn", Description = "40s Combed Cotton Yarn", PartNumber = "YRN40CCY", Make = "AriyAI Mills", Model = "Premium-40", Rate = 285.00m },
                new() { Group = "Yarn", Description = "30s Carded Cotton Yarn", PartNumber = "YRN30CDY", Make = "AriyAI Mills", Model = "Standard-30", Rate = 245.00m },
                new() { Group = "Thread", Description = "Polyester Sewing Thread 5000m", PartNumber = "THR-PLY-5K", Make = "ThreadCo", Model = "T-500", Rate = 65.50m },
                new() { Group = "Fabric", Description = "Grey Knitted Fabric (Single Jersey)", PartNumber = "FAB-SJ-G", Make = "AriyAI Weave", Model = "SJG-180", Rate = 320.00m },
                new() { Group = "Cotton", Description = "Organic Raw Cotton (Bales)", PartNumber = "COT-RAW-ORG", Make = "AgriCotton", Model = "Bale-Org-A", Rate = 15000.00m }
            };
            context.Products.AddRange(products);
            context.SaveChanges();

            // Helpers to find entities
            Agent GetAgent(string name) => agents.First(a => a.Name == name);
            Customer GetCust(string name) => customers.First(c => c.Name.StartsWith(name));

            // 4. Seed Sales Enquiries (Screenshot 1)
            var enquiries = new List<SalesEnquiry>
            {
                new() {
                    EnquiryNumber = "ENQ042600024",
                    EnquiryDate = DateTime.Now.AddDays(-85),
                    CustomerId = GetCust("Sri Manjunatha").Id,
                    AgentId = GetAgent("N.JAYAPRAKASH").Id,
                    Source = "Agent",
                    LeadType = "Cold",
                    Address = GetCust("Sri Manjunatha").Address,
                    AssignToId = GetAgent("N.JAYAPRAKASH").Id,
                    ExpiryDate = DateTime.Now.AddDays(30),
                    CustomerCountry = "India",
                    Remarks = "Urgent requirement for cotton yarn samples.",
                    Status = "Pending",
                    Aging = 85,
                    EnquiryProducts = new List<EnquiryProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", PartNumber = "YRN40CCY", Make = "AriyAI Mills", Model = "Premium-40", Quantity = 2000, Rate = 285.00m },
                        new() { Group = "Yarn", ProductDescription = "30s Carded Cotton Yarn", PartNumber = "YRN30CDY", Make = "AriyAI Mills", Model = "Standard-30", Quantity = 1000, Rate = 245.00m }
                    }
                },
                new() {
                    EnquiryNumber = "ENQ042600030",
                    EnquiryDate = DateTime.Now.AddDays(-85),
                    CustomerId = GetCust("Pasupati").Id,
                    AgentId = GetAgent("GM MARKETING").Id,
                    Source = "Direct",
                    LeadType = "Warm",
                    Address = GetCust("Pasupati").Address,
                    AssignToId = GetAgent("GM MARKETING").Id,
                    ExpiryDate = DateTime.Now.AddDays(15),
                    CustomerCountry = "India",
                    Remarks = "Standard request.",
                    Status = "Pending",
                    Aging = 85,
                    EnquiryProducts = new List<EnquiryProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", PartNumber = "YRN40CCY", Make = "AriyAI Mills", Model = "Premium-40", Quantity = 5000, Rate = 280.00m }
                    }
                },
                new() {
                    EnquiryNumber = "ENQ042600033",
                    EnquiryDate = DateTime.Now.AddDays(-85),
                    CustomerId = GetCust("Pongalur").Id,
                    AgentId = GetAgent("U. THALAIMALAI").Id,
                    Source = "Agent",
                    LeadType = "Hot",
                    Address = GetCust("Pongalur").Address,
                    AssignToId = GetAgent("U. THALAIMALAI").Id,
                    ExpiryDate = DateTime.Now.AddDays(10),
                    CustomerCountry = "India",
                    Remarks = "Multiple products requested.",
                    Status = "Pending",
                    Aging = 85,
                    EnquiryProducts = new List<EnquiryProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", Quantity = 1000, Rate = 285.00m },
                        new() { Group = "Yarn", ProductDescription = "30s Carded Cotton Yarn", Quantity = 1500, Rate = 245.00m },
                        new() { Group = "Thread", ProductDescription = "Polyester Sewing Thread 5000m", Quantity = 100, Rate = 65.50m },
                        new() { Group = "Fabric", ProductDescription = "Grey Knitted Fabric", Quantity = 500, Rate = 320.00m }
                    }
                },
                new() {
                    EnquiryNumber = "ENQ042600042",
                    EnquiryDate = DateTime.Now.AddDays(-84),
                    CustomerId = GetCust("S.P.APPARELS").Id,
                    AgentId = GetAgent("STS MARKETING").Id,
                    Source = "Email",
                    LeadType = "Cold",
                    Address = GetCust("S.P.APPARELS").Address,
                    AssignToId = GetAgent("STS MARKETING").Id,
                    Status = "Pending",
                    Aging = 84,
                    EnquiryProducts = new List<EnquiryProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", Quantity = 3000, Rate = 285.00m }
                    }
                },
                new() {
                    EnquiryNumber = "ENQ042600088",
                    EnquiryDate = DateTime.Now.AddDays(-75),
                    CustomerId = GetCust("SUMANLAL").Id,
                    AgentId = GetAgent("AJITH").Id,
                    Source = "Agent",
                    LeadType = "Warm",
                    Address = GetCust("SUMANLAL").Address,
                    AssignToId = GetAgent("AJITH").Id,
                    Status = "Pending",
                    Aging = 75,
                    EnquiryProducts = new List<EnquiryProduct>
                    {
                        new() { Group = "Thread", ProductDescription = "Polyester Sewing Thread 5000m", Quantity = 500, Rate = 62.00m }
                    }
                }
            };
            context.SalesEnquiries.AddRange(enquiries);
            context.SaveChanges();

            // 5. Seed Quotations (Screenshot 4)
            var quotations = new List<Quotation>
            {
                new() {
                    QuotationNumber = "QU0072600278",
                    QuotationDate = DateTime.Now,
                    CustomerReference = "REF-AK-098",
                    Currency = "INR",
                    DueDate = DateTime.Now.AddDays(15),
                    CustomerId = GetCust("A.K ENTERPRISES").Id,
                    Address = GetCust("A.K ENTERPRISES").Address,
                    AgentId = GetAgent("AJITH").Id,
                    Subject1 = "Quotation for Combed Cotton Yarn",
                    Subject2 = "Ref Enquiry: ENQ-26782",
                    Status = "Pending",
                    Aging = 0,
                    QuotationProducts = new List<QuotationProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", PartNumber = "YRN40CCY", Make = "AriyAI Mills", Model = "Premium-40", Quantity = 10000, Rate = 282.50m }
                    }
                },
                new() {
                    QuotationNumber = "QU0072600277",
                    QuotationDate = DateTime.Now,
                    CustomerReference = "REF-AK-097",
                    Currency = "INR",
                    DueDate = DateTime.Now.AddDays(15),
                    CustomerId = GetCust("A.K ENTERPRISES").Id,
                    Address = GetCust("A.K ENTERPRISES").Address,
                    AgentId = GetAgent("AJITH").Id,
                    Subject1 = "Quotation for Carded Cotton Yarn",
                    Status = "Pending",
                    Aging = 0,
                    QuotationProducts = new List<QuotationProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "30s Carded Cotton Yarn", PartNumber = "YRN30CDY", Make = "AriyAI Mills", Model = "Standard-30", Quantity = 5000, Rate = 243.00m }
                    }
                },
                new() {
                    QuotationNumber = "QU0072600276",
                    QuotationDate = DateTime.Now,
                    CustomerReference = "JM-ENQ-88",
                    Currency = "INR",
                    DueDate = DateTime.Now.AddDays(30),
                    CustomerId = GetCust("JOHN MILTON").Id,
                    Address = GetCust("JOHN MILTON").Address,
                    AgentId = GetAgent("U. THALAIMALAI").Id,
                    Subject1 = "Supply of Sewing Thread",
                    Status = "Pending",
                    Aging = 0,
                    QuotationProducts = new List<QuotationProduct>
                    {
                        new() { Group = "Thread", ProductDescription = "Polyester Sewing Thread 5000m", PartNumber = "THR-PLY-5K", Make = "ThreadCo", Model = "T-500", Quantity = 200, Rate = 65.00m }
                    }
                },
                new() {
                    QuotationNumber = "QU0062600275",
                    QuotationDate = DateTime.Now.AddDays(-1),
                    CustomerReference = "4S-RFQ-992",
                    Currency = "INR",
                    DueDate = DateTime.Now.AddDays(10),
                    CustomerId = GetCust("4S Spintex").Id,
                    Address = GetCust("4S Spintex").Address,
                    AgentId = GetAgent("N.JAYAPRAKASH").Id,
                    Subject1 = "Cotton Yarn & Sewing Thread Supply",
                    Status = "Pending",
                    Aging = 1,
                    QuotationProducts = new List<QuotationProduct>
                    {
                        new() { Group = "Yarn", ProductDescription = "40s Combed Cotton Yarn", Quantity = 15000, Rate = 285.00m },
                        new() { Group = "Thread", ProductDescription = "Polyester Sewing Thread 5000m", Quantity = 500, Rate = 64.00m }
                    }
                }
            };
            context.Quotations.AddRange(quotations);
            context.SaveChanges();
        }
    }
}
