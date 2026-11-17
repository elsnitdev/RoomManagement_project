using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ContractDocumentServiceTests
{
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid ContractId = Guid.NewGuid();
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];

    [Fact]
    public async Task Draft_accepts_images_but_rejects_mixing_a_pdf()
    {
        var store = new FakeDocuments();
        var storage = new FakeStorage();
        var service = Create(store, storage);

        var first = await service.UploadAsync(PropertyId, ContractId, "page.png", Png.Length,
            new MemoryStream(Png), CancellationToken.None);
        Assert.Equal("image/png", first.FileType);
        Assert.Equal(0, first.PageOrder);
        Assert.Equal(Png, storage.Files[first.StorageKey]);
        Assert.Equal(64, first.Sha256!.Length);

        var second = await service.UploadAsync(PropertyId, ContractId, "page2.png", Png.Length,
            new MemoryStream(Png), CancellationToken.None);
        Assert.Equal(1, second.PageOrder);

        var pdf = "%PDF-test"u8.ToArray();
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            PropertyId, ContractId, "signed.pdf", pdf.Length, new MemoryStream(pdf), CancellationToken.None));
        Assert.Equal("CONTRACT_DOCUMENT_FORMAT_MIXED", error.Code);
        Assert.Equal(2, store.Items.Count);
    }

    [Fact]
    public async Task Non_draft_contract_cannot_change_sent_documents()
    {
        var store = new FakeDocuments();
        store.Contract.Status = ContractStatus.PendingTenantConfirmation;
        var service = Create(store, new FakeStorage());
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            PropertyId, ContractId, "page.png", Png.Length, new MemoryStream(Png), CancellationToken.None));
        Assert.Equal("CONTRACT_DOCUMENT_LOCKED", error.Code);
    }

    [Fact]
    public async Task Single_pdf_is_accepted_and_unknown_file_is_rejected()
    {
        var store = new FakeDocuments();
        var service = Create(store, new FakeStorage());
        var pdf = "%PDF-test"u8.ToArray();
        var document = await service.UploadAsync(PropertyId, ContractId, "signed.pdf", pdf.Length,
            new MemoryStream(pdf), CancellationToken.None);
        Assert.Equal("application/pdf", document.FileType);

        var unknown = "not an image"u8.ToArray();
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            PropertyId, ContractId, "fake.png", unknown.Length, new MemoryStream(unknown), CancellationToken.None));
        Assert.Equal("CONTRACT_DOCUMENT_FORMAT_INVALID", error.Code);
        Assert.Single(store.Items);
    }

    [Fact]
    public async Task Tenant_sees_only_documents_from_current_offer_version()
    {
        var store = new FakeDocuments { VisibleOfferVersion = 2 };
        store.Items.Add(new ContractDocument { Id = Guid.NewGuid(), ContractId = ContractId,
            DocumentType = ContractDocumentType.ContractPage, OfferVersion = 1, FileName = "old.png", StorageKey = "old" });
        store.Items.Add(new ContractDocument { Id = Guid.NewGuid(), ContractId = ContractId,
            DocumentType = ContractDocumentType.ContractPage, OfferVersion = 2, FileName = "new.png", StorageKey = "new" });
        var result = await Create(store, new FakeStorage()).TenantListAsync(ContractId, "tenant", CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("new.png", result[0].FileName);
    }

    private static ContractDocumentService Create(FakeDocuments documents, FakeStorage storage) =>
        new(new FakeCurrent(), new FakeLandlords(), documents, storage);

    private sealed class FakeCurrent : ICurrentLandlord
    { public string GetUserId() => "owner"; }

    private sealed class FakeLandlords : ILandlordStore
    { public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken ct) => Task.FromResult(true); }

    private sealed class FakeDocuments : IContractDocumentStore
    {
        public Contract Contract { get; } = new() { Id = ContractId, Workflow = ContractWorkflow.Invitation,
            Status = ContractStatus.Draft, ContractCode = "TEST" };
        public int? VisibleOfferVersion { get; set; } = 1;
        public List<ContractDocument> Items { get; } = [];
        public Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
            Task.FromResult<Contract?>(propertyId == PropertyId && contractId == ContractId && ownerId == "owner" ? Contract : null);
        public Task<int?> TenantVisibleOfferVersionAsync(Guid contractId, string userId, CancellationToken ct) =>
            Task.FromResult(contractId == ContractId && userId == "tenant" ? VisibleOfferVersion : null);
        public Task<IReadOnlyList<ContractDocument>> ListAsync(Guid contractId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ContractDocument>>(Items);
        public Task<ContractDocument?> GetAsync(Guid contractId, Guid documentId, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == documentId));
        public Task AddAsync(ContractDocument document, CancellationToken ct)
        { Items.Add(document); return Task.CompletedTask; }
        public Task DeleteDraftAsync(ContractDocument document, CancellationToken ct)
        { Items.Remove(document); return Task.CompletedTask; }
    }

    private sealed class FakeStorage : IObjectStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct)
        { using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, ct); Files[name] = buffer.ToArray(); }
        public Task<Stream> OpenReadAsync(string name, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(Files[name]));
        public Task DeleteAsync(string name, CancellationToken ct)
        { Files.Remove(name); return Task.CompletedTask; }
    }
}
