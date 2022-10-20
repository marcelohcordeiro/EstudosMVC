using AppMvcBasica.Models;
using AutoMapper;
using DevIO.App.Extensions;
using DevIO.App.ViewModels;
using DevIO.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DevIO.App.Controllers
{
    [Authorize]
    public class ProdutosController : BaseController
    {
        private readonly IProdutoRepository _produtoRepository;
        private readonly IProdutoService _produtoService;
        private readonly IFornecedorRepository _fornecedorRepository;
        private readonly INotificador _notificador;
        private readonly IMapper _mapper;
        public ProdutosController(IProdutoRepository produtoRepository,
                                    IProdutoService produtoService,
                                    IFornecedorRepository fornecedorRepository,
                                    IMapper mapper,
                                    INotificador notificador) : base(notificador)
        {
            _produtoRepository = produtoRepository;
            _produtoService = produtoService;
            _fornecedorRepository = fornecedorRepository;
            _mapper = mapper;
        }

        [AllowAnonymous]
        // GET: ProdutosController
        [Route("lista-de-produtos")]
        public async Task<ActionResult> Index()
        {

            return View(_mapper.Map<IEnumerable<ProdutoViewModel>>(await _produtoRepository.ObterProdutosFornecedores()));
        }

        [AllowAnonymous]
        // GET: ProdutosController/Details/5
        [Route("dados-do-produto/{id:guid}")]
        public async Task<ActionResult> Details(Guid id)
        {
            var produtoViewModel = await ObterProduto(id);

            if(produtoViewModel == null)
            {
                return NotFound();
            }

            return View(produtoViewModel);
        }

        [ClaimsAuthorize("Produto","Adicionar")]
        [Route("novo-produto")]
        public async Task<ActionResult> Create()
        {
            var produtoViewModel = await PopularFornecedores(new ProdutoViewModel());
            return View(produtoViewModel);

        }

        [ClaimsAuthorize("Produto", "Adicionar")]
        // POST: ProdutosController/Create
        [Route("novo-produto")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ProdutoViewModel produtoViewModel)
        {
            produtoViewModel = await PopularFornecedores(produtoViewModel);
            

            if (!ModelState.IsValid)
                return View(produtoViewModel);

            //prefixo para montar o nome da imagem
            var imgPrefixo = Guid.NewGuid() + "_";
            
            //verificando se foi feito o upload com sucesso
            if(!await UploadArquivo(produtoViewModel.ImagemUpload, imgPrefixo))
            {
                return View(produtoViewModel);
            }

            //Populando o nome da Imagem
            produtoViewModel.Imagem = imgPrefixo + produtoViewModel.ImagemUpload.FileName;

            var Produto = _mapper.Map<Produto>(produtoViewModel);
            await _produtoService.Adicionar(Produto);

            if(!OperacaoValida())
                return View(produtoViewModel);

            return RedirectToAction("Index");

        }

        [ClaimsAuthorize("Produto", "Editar")]
        [Route("editar-produto/{id:guid}")]
        public async Task<ActionResult> Edit(Guid id)
        {
            var produtoViewModel = await ObterProduto(id);

            if(produtoViewModel == null)
                return NotFound();

            return View(produtoViewModel);
        }

        [ClaimsAuthorize("Produto", "Editar")]
        [Route("editar-produto/{id:guid}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(Guid id, ProdutoViewModel produtoViewModel)
        {
            if (id != produtoViewModel.Id)
                return NotFound();

            //ajuste para caso o ModelState seja inválido,
            //retornar as informações antigas(que estão no BD) sem as modificações feitas no disparo do HttpPost
            var produtoAtualizacao = await ObterProduto(id);
            produtoViewModel.Fornecedor = produtoAtualizacao.Fornecedor;
            produtoViewModel.FornecedorId = produtoAtualizacao.FornecedorId;
            produtoViewModel.Imagem = produtoAtualizacao.Imagem;


            if (!ModelState.IsValid)
            {
                return View(produtoViewModel);
            }
            
            if(produtoViewModel.ImagemUpload != null)
            {
                //prefixo para montar o nome da imagem
                var imgPrefixo = Guid.NewGuid() + "_";

                //verificando se foi feito o upload com sucesso
                if (!await UploadArquivo(produtoViewModel.ImagemUpload, imgPrefixo))
                {
                    return View(produtoViewModel);
                }

                //Populando o nome da Imagem
                produtoAtualizacao.Imagem = imgPrefixo + produtoViewModel.ImagemUpload.FileName;
            }

            produtoAtualizacao.Nome = produtoViewModel.Nome;
            produtoAtualizacao.Descricao = produtoViewModel.Descricao;
            produtoAtualizacao.Valor = produtoViewModel.Valor;
            produtoAtualizacao.Ativo = produtoViewModel.Ativo;

            var Produto = _mapper.Map<Produto>(produtoAtualizacao);
            await _produtoService.Atualizar(Produto);

            if (!OperacaoValida())
                return View(produtoViewModel);

            return RedirectToAction("Index");
        }



        [ClaimsAuthorize("Produto", "Excluir")]
        // GET: ProdutosController/Delete/5
        [Route("excluir-produto/{id:guid}")]

        public async Task<ActionResult> Delete(Guid id)
        {
            var produtoViewModel = await ObterProduto(id);

            if(produtoViewModel ==null)
                return NotFound();

            return View(produtoViewModel);
        }

        [ClaimsAuthorize("Produto", "Excluir")]    
        [Route("excluir-produto/{id:guid}")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(Guid id, IFormCollection collection)
        {
            
            var produtoViewModel = await ObterProduto(id);

            if (produtoViewModel == null)
                return NotFound();

            await _produtoService.Remover(id);

            if (!OperacaoValida())            
                return View(produtoViewModel);


            TempData["Sucesso"] = "Produto excluido com sucesso!";

            return RedirectToAction("Index");

        }

        private async Task<ProdutoViewModel> ObterProduto(Guid id)
        {
            var produto = _mapper.Map<ProdutoViewModel>(await _produtoRepository.ObterProdutoFornecedor(id));
            produto.Fornecedores = _mapper.Map<IEnumerable<FornecedorViewModel>>(await _fornecedorRepository.ObterTodos());
            return produto;
        }

        private async Task<ProdutoViewModel> PopularFornecedores(ProdutoViewModel produto)
        {

            produto.Fornecedores = _mapper.Map<IEnumerable<FornecedorViewModel>>(await _fornecedorRepository.ObterTodos());
            return produto;
        }

        private async Task<bool> UploadArquivo(IFormFile arquivo, string imgPrefixo)
        {
            if(arquivo.Length <= 0)
                return false;

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/imagens", imgPrefixo + arquivo.FileName);

            if (System.IO.File.Exists(path))
            {
                ModelState.AddModelError(string.Empty, "Já existe um arquivo com este nome!");
                return false;
            }

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }

            return true;

        }

    }
}
