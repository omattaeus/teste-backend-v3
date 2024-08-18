﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TheatricalPlayersRefactoringKata.Application.Request;
using TheatricalPlayersRefactoringKata.Core.Entities;
using TheatricalPlayersRefactoringKata.Core.Interfaces;
using TheatricalPlayersRefactoringKata.Data.Dto;

namespace TheatricalPlayersRefactoringKata.Infrastructure
{
    public class StatementProcessingService
    {
        private readonly BlockingCollection<InvoiceRequest> _invoiceQueue = new BlockingCollection<InvoiceRequest>();
        private readonly IStatementGenerator _statementGenerator;
        private readonly string _outputDirectory;
        private readonly ILogger<StatementProcessingService> _logger;

        public StatementProcessingService(
            IStatementGenerator statementGenerator,
            string outputDirectory,
            ILogger<StatementProcessingService> logger)
        {
            _statementGenerator = statementGenerator ?? throw new ArgumentNullException(nameof(statementGenerator));
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Directory.CreateDirectory(_outputDirectory);

            Task.Run(() => ProcessQueueAsync());
        }

        public void QueueInvoice(InvoiceRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "O parâmetro 'request' não pode ser nulo.");

            _invoiceQueue.Add(request);
        }

        private async Task ProcessQueueAsync()
        {
            foreach (var request in _invoiceQueue.GetConsumingEnumerable())
            {
                try
                {
                    var playDictionary = request.Plays.ToDictionary(p => p.PlayId, p => p);

                    var xmlInvoice = MapToXmlInvoice(request.Invoice, playDictionary);
                    var xmlContent = _statementGenerator.Generate(request.Invoice, playDictionary); // Alterado para usar Dictionary diretamente
                    var filePath = Path.Combine(_outputDirectory, $"{request.Invoice.Customer}.xml");

                    await File.WriteAllTextAsync(filePath, xmlContent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar o extrato para o cliente '{Customer}' com ID '{InvoiceId}'.",
                        request.Invoice.Customer, request.Invoice.Id);

                    throw new InvalidOperationException("Ocorreu um erro ao processar o extrato.", ex);
                }
            }
        }

        private XmlInvoice MapToXmlInvoice(Invoice invoice, Dictionary<Guid, Play> playDictionary)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            return new XmlInvoice
            {
                Customer = invoice.Customer,
                TotalAmount = invoice.TotalAmount,
                TotalCredits = invoice.TotalCredits,
                Performances = invoice.Performances.Select(p => new XmlPerformance
                {
                    PlayId = p.PlayId,
                    Audience = p.Audience,
                    Genre = p.Genre.ToString()
                }).ToList()
            };
        }
    }
}