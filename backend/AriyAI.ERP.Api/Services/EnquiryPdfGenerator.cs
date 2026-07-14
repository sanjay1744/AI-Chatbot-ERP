using System;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Services
{
    public static class EnquiryPdfGenerator
    {
        public static byte[] GenerateAcknowledgementPdf(SalesEnquiry enquiry)
        {
            // Register QuestPDF Community License
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Content().Border(1).Padding(15).Column(column =>
                    {
                        // 1. Header (Company details)
                        column.Item().Text("NAREN TEXTILE ENGINEERS INDIA PVT.LTD.").Bold().FontSize(14).AlignCenter();
                        column.Item().Text("9/10, Periar Nagar, Nehru Nagar East, Civil Aerodrome Post, Coimbatore-641 014").FontSize(9).AlignCenter();
                        column.Item().Text("Phone : 0422-2967127, 9842216021, 9842216025 Tele Fax : 0422-2967128").FontSize(9).AlignCenter();
                        column.Item().Text("Email : customerssupport@narenonline.com, www.narengroup.in").FontSize(9).AlignCenter();
                        column.Item().Text("GST : 33AABCN9439N1ZZ CIN : U29291TZ2003PTC010710").FontSize(9).AlignCenter();
                        
                        column.Item().PaddingVertical(5).LineHorizontal(1);

                        // 2. Title and Info columns
                        column.Item().Row(row =>
                        {
                            // Left - Title and customer info
                            row.RelativeItem().Column(infoCol =>
                            {
                                infoCol.Item().Text("SALES ENQUIRY").Bold().FontSize(12);
                                infoCol.Item().PaddingTop(5).Text("To : M/s.").Bold();
                                infoCol.Item().Text(enquiry.Customer?.Name ?? "SUMANLAL J. SHAH & CO(sales)").Bold();
                                infoCol.Item().Text(enquiry.Address ?? "From Email Direct\nINDIA\nContact Number : —");
                            });

                            // Right - Enquiry numbers and dates
                            row.ConstantItem(180).Column(dateCol =>
                            {
                                dateCol.Item().Text($"No : {enquiry.EnquiryNumber}").Bold();
                                dateCol.Item().Text($"Date : {enquiry.EnquiryDate:dd/MMM/yyyy}");
                                dateCol.Item().Text($"Valid Upto : {enquiry.ExpiryDate?.ToString("dd/MMM/yyyy") ?? enquiry.EnquiryDate.ToString("dd/MMM/yyyy")}");
                            });
                        });

                        column.Item().PaddingVertical(5).LineHorizontal(0.5f);

                        // 3. Attn and Subject Info
                        column.Item().Column(attnCol =>
                        {
                            attnCol.Item().Text(t => {
                                t.Span("Kind Attn : ").Bold();
                                t.Span(enquiry.Customer?.Name ?? "Sanjay S");
                            });
                            attnCol.Item().Text(t => {
                                t.Span("Subject : ").Bold();
                                t.Span("Sales Enquiry - Reg");
                            });
                            attnCol.Item().Text(t => {
                                t.Span("Reference : ").Bold();
                                t.Span(enquiry.Remarks ?? "");
                            });
                        });

                        column.Item().PaddingVertical(5).LineHorizontal(0.5f);

                        // 4. Salutation
                        column.Item().Text("Dear Sir,").Bold();
                        column.Item().Text("With reference to the above, we are pleased to record your enquiry details as given below:");
                        column.Item().PaddingBottom(5);

                        // 5. Products Table
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // Sl.
                                columns.RelativeColumn();    // Description
                                columns.ConstantColumn(70);  // HSN
                                columns.ConstantColumn(50);  // Qty
                                columns.ConstantColumn(40);  // Unit
                            });

                            // Header Row
                            table.Header(header =>
                            {
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text("Sl.").Bold();
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4).Text("Description of Items").Bold();
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text("HSN").Bold();
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text("Qty").Bold();
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text("Unit").Bold();
                            });

                            // Data Rows
                            int index = 1;
                            int totalQty = 0;
                            foreach (var prod in enquiry.EnquiryProducts)
                            {
                                table.Cell().Border(0.5f).Padding(4).AlignCenter().Text(index.ToString());
                                
                                string description = prod.ProductDescription;
                                if (!string.IsNullOrEmpty(prod.PartNumber) && !description.Contains(prod.PartNumber))
                                {
                                    description = $"{prod.PartNumber} - {description}";
                                }
                                table.Cell().Border(0.5f).Padding(4).Text(description);
                                table.Cell().Border(0.5f).Padding(4).AlignCenter().Text("84483990");
                                table.Cell().Border(0.5f).Padding(4).AlignCenter().Text(prod.Quantity.ToString());
                                table.Cell().Border(0.5f).Padding(4).AlignCenter().Text("PCS");
                                
                                totalQty += prod.Quantity;
                                index++;
                            }

                            // Total Row
                            table.Cell().Border(0.5f).Padding(4).Text("Total").Bold();
                            table.Cell().Border(0.5f).Padding(4).Text("");
                            table.Cell().Border(0.5f).Padding(4).Text("");
                            table.Cell().Border(0.5f).Padding(4).AlignCenter().Text(totalQty.ToString()).Bold();
                            table.Cell().Border(0.5f).Padding(4).Text("");
                        });

                        // 6. Terms and Conditions
                        column.Item().PaddingTop(10).Text("Terms & Conditions").Bold();
                        column.Item().Text("1. Subject to Coimbatore Jurisdiction.");
                        column.Item().Text("2. This is an enquiry acknowledgement copy.");

                        // 7. Signature block
                        column.Item().PaddingTop(15).AlignRight().Column(sigCol =>
                        {
                            sigCol.Item().Text("For NAREN TEXTILE ENGINEERS INDIA PVT.LTD.").Bold();
                            sigCol.Item().PaddingTop(25).Text("Authorized Signatory").FontSize(9);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}
