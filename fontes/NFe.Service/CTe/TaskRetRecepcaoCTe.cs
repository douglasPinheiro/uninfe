﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Xml;
using NFe.Components;
using NFe.Settings;
using NFe.Certificado;
using NFe.Exceptions;

namespace NFe.Service
{
    public class TaskCTeRetRecepcao : TaskAbst
    {
        public TaskCTeRetRecepcao()
        {
            Servico = Servicos.CTePedidoSituacaoLote;
        }

        #region Classe com os dados do XML do pedido de consulta do recibo do lote de nfe enviado
        /// <summary>
        /// Esta Herança que deve ser utilizada fora da classe para obter os valores das tag´s do pedido de consulta do recibo do lote de NFe enviado
        /// </summary>
        private DadosPedRecClass dadosPedRec;
        #endregion

        #region Execute
        public override void Execute()
        {
            int emp = Empresas.FindEmpresaByThread();

            try
            {
                #region Parte do código que envia o XML de pedido de consulta do recibo
                dadosPedRec = new DadosPedRecClass();
                PedRec(emp, NomeArquivoXML);

                //Definir o objeto do WebService
                WebServiceProxy wsProxy = ConfiguracaoApp.DefinirWS(Servico,  emp, dadosPedRec.cUF, dadosPedRec.tpAmb, dadosPedRec.tpEmis);

                //Criar objetos das classes dos serviços dos webservices do SEFAZ
                var oRepRecepcao = wsProxy.CriarObjeto(wsProxy.NomeClasseWS);//NomeClasseWS(Servico, dadosPedRec.cUF));
                var oCabecMsg = wsProxy.CriarObjeto(NomeClasseCabecWS(dadosPedRec.cUF, Servico));

                //Atribuir conteúdo para duas propriedades da classe nfeCabecMsg
                wsProxy.SetProp(oCabecMsg, NFe.Components.TpcnResources.cUF.ToString(), dadosPedRec.cUF.ToString());
                wsProxy.SetProp(oCabecMsg, NFe.Components.TpcnResources.versaoDados.ToString(), NFe.ConvertTxt.versoes.VersaoXMLCTePedRec);

                //Invocar o método que envia o XML para o SEFAZ
                oInvocarObj.Invocar(wsProxy, 
                                    oRepRecepcao, 
                                    wsProxy.NomeMetodoWS[0],//NomeMetodoWS(Servico, dadosPedRec.cUF), 
                                    oCabecMsg, this);
                #endregion

                #region Parte do código que trata o XML de retorno da consulta do recibo
                //Efetuar a leituras das notas do lote para ver se foi autorizada ou não
                LerRetornoLoteCTe();

                //Gravar o XML retornado pelo WebService do SEFAZ na pasta de retorno para o ERP
                //Tem que ser feito neste ponto, pois somente aqui terminamos todo o processo
                //Wandrey 18/06/2009
                oGerarXML.XmlRetorno(Propriedade.ExtEnvio.PedRec_XML, Propriedade.ExtRetorno.ProRec_XML, vStrXmlRetorno);
                #endregion
            }
            catch (Exception ex)
            {
                try
                {
                    TFunctions.GravarArqErroServico(NomeArquivoXML, Propriedade.ExtEnvio.PedRec_XML, Propriedade.ExtRetorno.ProRec_ERR, ex);
                }
                catch
                {
                    //Se falhou algo na hora de gravar o retorno .ERR (de erro) para o ERP, infelizmente não posso fazer mais nada.
                    //Pois ocorreu algum erro de rede, hd, permissão das pastas, etc. Wandrey 22/03/2010
                }
            }
            finally
            {
                //Deletar o arquivo de solicitação do serviço
                Functions.DeletarArquivo(NomeArquivoXML);
            }
        }
        #endregion

        #region PedRec()
        /// <summary>
        /// Faz a leitura do XML de pedido da consulta do recibo do lote de notas enviadas
        /// </summary>
        /// <param name="cArquivoXml">Nome do XML a ser lido</param>
        /// <remarks>
        /// Autor: Wandrey Mundin Ferreira
        /// Data: 16/03/2010
        /// </remarks>
        private void PedRec(int emp, string cArquivoXML)
        {
            this.dadosPedRec.tpAmb = 0;
            this.dadosPedRec.tpEmis = Empresas.Configuracoes[emp].tpEmis;
            this.dadosPedRec.cUF = Empresas.Configuracoes[emp].UnidadeFederativaCodigo;
            this.dadosPedRec.nRec = string.Empty;

            XmlDocument doc = new XmlDocument();
            doc.Load(cArquivoXML);

            XmlNodeList consReciNFeList = doc.GetElementsByTagName("consReciCTe");

            foreach (XmlNode consReciNFeNode in consReciNFeList)
            {
                XmlElement consReciNFeElemento = (XmlElement)consReciNFeNode;

                dadosPedRec.tpAmb = Convert.ToInt32("0" + consReciNFeElemento.GetElementsByTagName(TpcnResources.tpAmb.ToString())[0].InnerText);
                dadosPedRec.nRec = consReciNFeElemento.GetElementsByTagName(TpcnResources.nRec.ToString())[0].InnerText;
                dadosPedRec.cUF = Convert.ToInt32(dadosPedRec.nRec.Substring(0, 2));

                if (consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.cUF.ToString()).Count != 0)
                {
                    this.dadosPedRec.cUF = Convert.ToInt32("0" + consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.cUF.ToString())[0].InnerText);
                    /// Para que o validador não rejeite, excluo a tag <cUF>
                    doc.DocumentElement.RemoveChild(consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.cUF.ToString())[0]);
                    /// Salvo o arquivo modificado
                    doc.Save(cArquivoXML);
                }
                if (consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.tpEmis.ToString()).Count != 0)
                {
                    this.dadosPedRec.tpEmis = Convert.ToInt16(consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.tpEmis.ToString())[0].InnerText);
                    /// Para que o validador não rejeite, excluo a tag <tpEmis>
                    doc.DocumentElement.RemoveChild(consReciNFeElemento.GetElementsByTagName(NFe.Components.TpcnResources.tpEmis.ToString())[0]);
                    /// Salvo o arquivo modificado
                    doc.Save(cArquivoXML);
                }
            }

        }
        #endregion

        #region LerRetornoLoteCTe()
        /// <summary>
        /// Efetua a leitura do XML de retorno do processamento do lote de notas fiscais e 
        /// atualiza o arquivo de fluxo e envio de notas
        /// </summary>
        /// <by>Wandrey Mundin Ferreira</by>
        /// <date>20/04/2009</date>
        private void LerRetornoLoteCTe()
        {
            int emp = Empresas.FindEmpresaByThread();
            var oLerXml = new LerXML();
            var msXml = Functions.StringXmlToStream(vStrXmlRetorno);
            var fluxoNFe = new FluxoNfe();

            var doc = new System.Xml.XmlDocument();
            doc.Load(msXml);

            var retConsReciNFeList = doc.GetElementsByTagName("retConsReciCTe");

            foreach (System.Xml.XmlNode retConsReciNFeNode in retConsReciNFeList)
            {
                var retConsReciNFeElemento = (System.Xml.XmlElement)retConsReciNFeNode;

                //Pegar o número do recibo do lote enviado
                var nRec = string.Empty;
                if (retConsReciNFeElemento.GetElementsByTagName(TpcnResources.nRec.ToString())[0] != null)
                {
                    nRec = retConsReciNFeElemento.GetElementsByTagName(TpcnResources.nRec.ToString())[0].InnerText;
                }

                //Pegar o status de retorno do lote enviado
                var cStatLote = string.Empty;
                if (retConsReciNFeElemento.GetElementsByTagName(TpcnResources.cStat.ToString())[0] != null)
                {
                    cStatLote = retConsReciNFeElemento.GetElementsByTagName(TpcnResources.cStat.ToString())[0].InnerText;
                }

                switch (cStatLote)
                {
                    #region Rejeições do XML de consulta do recibo (Não é o lote que foi rejeitado e sim o XML de consulta do recibo)
                    case "280": //A-Certificado transmissor inválido
                    case "281": //A-Validade do certificado
                    case "283": //A-Verifica a cadeia de Certificação
                    case "286": //A-LCR do certificado de Transmissor
                    case "284": //A-Certificado do Transmissor revogado
                    case "285": //A-Certificado Raiz difere da "IPC-Brasil"
                    case "282": //A-Falta a extensão de CNPJ no Certificado
                    case "214": //B-Tamanho do XML de dados superior a 500 Kbytes
                    case "243": //B-XML de dados mal formatado
                    case "108": //B-Verifica se o Serviço está paralisado momentaneamente
                    case "109": //B-Verifica se o serviço está paralisado sem previsão
                    case "242": //C-Elemento nfeCabecMsg inexistente no SOAP Header
                    case "409": //C-Campo cUF inexistente no elemento nfeCabecMsg do SOAP Header
                    case "410": //C-Campo versaoDados inexistente no elemento nfeCabecMsg do SOAP
                    case "411": //C-Campo versaoDados inexistente no elemento nfeCabecMsg do SOAP
                    case "238": //C-Versão dos Dados informada é superior à versão vigente
                    case "239": //C-Versão dos Dados não suportada
                    case "215": //D-Verifica schema XML da área de dados
                    case "404": //D-Verifica o uso de prefixo no namespace
                    case "402": //D-XML utiliza codificação diferente de UTF-8
                    case "252": //E-Tipo do ambiente da NF-e difere do ambiente do web service
                    case "226": //E-UF da Chave de Acesso difere da UF do Web Service
                    case "236": //E-Valida DV da Chave de Acesso
                    case "217": //E-Acesso BD CTE
                    case "216": //E-Verificar se campo "Codigo Numerico"
                        break;
                    #endregion

                    #region Lote ainda está sendo processado
                    case "105": //E-Verifica se o lote não está na fila de resposta, mas está na fila de entrada (Lote em processamento)
                        //Ok vou aguardar o ERP gerar uma nova consulta para encerrar o fluxo da nota
                        break;
                    #endregion

                    #region Lote não foi localizado pelo recibo que está sendo consultado
                    case "106": //E-Verifica se o lote não está na fila de saída, nem na fila de entrada (Lote não encontrado)
                        //No caso do lote não encontrado através do recibo, o ERP vai ter que consultar a situação da NFe para encerrar ela
                        //Vou somente excluir ela do fluxo para não ficar consultando o recibo que não existe
                        if (nRec != string.Empty)
                        {
                            fluxoNFe.ExcluirNfeFluxoRec(nRec.Trim());
                        }
                        break;
                    #endregion

                    #region Lote foi processado, agora tenho que tratar as notas fiscais dele
                    case "104": //Lote processado
                        var protNFeList = retConsReciNFeElemento.GetElementsByTagName("protCTe");

                        foreach (System.Xml.XmlNode protNFeNode in protNFeList)
                        {
                            var protNFeElemento = (System.Xml.XmlElement)protNFeNode;

                            var strProtNfe = protNFeElemento.OuterXml;

                            var infProtList = protNFeElemento.GetElementsByTagName("infProt");

                            foreach (System.Xml.XmlNode infProtNode in infProtList)
                            {
                                var infProtElemento = (System.Xml.XmlElement)infProtNode;

                                var strChaveNFe = string.Empty;
                                var strStat = string.Empty;

                                if (infProtElemento.GetElementsByTagName(TpcnResources.chCTe.ToString())[0] != null)
                                {
                                    strChaveNFe = "CTe" + infProtElemento.GetElementsByTagName(TpcnResources.chCTe.ToString())[0].InnerText;
                                }

                                if (infProtElemento.GetElementsByTagName(TpcnResources.cStat.ToString())[0] != null)
                                {
                                    strStat = infProtElemento.GetElementsByTagName(TpcnResources.cStat.ToString())[0].InnerText;
                                }

                                //Definir o nome do arquivo da NFe e seu caminho
                                var strNomeArqNfe = fluxoNFe.LerTag(strChaveNFe, FluxoNfe.ElementoFixo.ArqNFe);

                                // se por algum motivo o XML não existir no "Fluxo", então o arquivo tem que existir
                                // na pasta "EmProcessamento" assinada.
                                if (string.IsNullOrEmpty(strNomeArqNfe))
                                {
                                    if (string.IsNullOrEmpty(strChaveNFe))
                                        throw new Exception("LerRetornoLoteCTe(): Não pode obter o nome do arquivo");

                                    strNomeArqNfe = strChaveNFe.Substring(3) + Propriedade.ExtEnvio.Cte;
                                }
                                var strArquivoNFe = Empresas.Configuracoes[emp].PastaXmlEnviado + "\\" + PastaEnviados.EmProcessamento.ToString() + "\\" + strNomeArqNfe;

                                //Atualizar a Tag de status da NFe no fluxo para que se ocorrer alguma falha na exclusão eu tenha esta campo para ter uma referencia em futuras consultas
                                fluxoNFe.AtualizarTag(strChaveNFe, FluxoNfe.ElementoEditavel.cStat, strStat);

                                //Atualizar a tag da data e hora da ultima consulta do recibo
                                fluxoNFe.AtualizarDPedRec(nRec, DateTime.Now);

                                switch (strStat)
                                {
                                    case "100": //NFe Autorizada
                                        if (File.Exists(strArquivoNFe))
                                        {
                                            //Juntar o protocolo com a NFE já copiando para a pasta em processamento
                                            var strArquivoNFeProc = oGerarXML.XmlDistCTe(strArquivoNFe, strProtNfe);
                                            
                                            //Ler o XML para pegar a data de emissão para criar a pasta dos XML´s autorizados
                                            oLerXml.Cte(strArquivoNFe);

                                            //Mover a cteProc da pasta de CTe em processamento para a NFe Autorizada
                                            //Para envitar falhar, tenho que mover primeiro o XML de distribuição (-procCTe.xml) para
                                            //depois mover o da nfe (-cte.xml), pois se ocorrer algum erro, tenho como reconstruir o senário, 
                                            //assim sendo não inverta as posições. Wandrey 08/10/2009
                                            TFunctions.MoverArquivo(strArquivoNFeProc, PastaEnviados.Autorizados, oLerXml.oDadosNfe.dEmi);

                                            //Mover a CTe da pasta de CTe em processamento para CTe Autorizada
                                            //Para envitar falhar, tenho que mover primeiro o XML de distribuição (-procCTe.xml) para 
                                            //depois mover o da nfe (-cte.xml), pois se ocorrer algum erro, tenho como reconstruir o cenário.
                                            //assim sendo não inverta as posições. Wandrey 08/10/2009
                                            TFunctions.MoverArquivo(strArquivoNFe, PastaEnviados.Autorizados, oLerXml.oDadosNfe.dEmi);

                                            //Disparar a geração/impressao do UniDanfe. 03/02/2010 - Wandrey
                                            try
                                            {
                                                var strArquivoDist = Empresas.Configuracoes[emp].PastaXmlEnviado + "\\" +
                                                                        PastaEnviados.Autorizados.ToString() + "\\" +
                                                                        Empresas.Configuracoes[emp].DiretorioSalvarComo.ToString(oLerXml.oDadosNfe.dEmi) +
                                                                        Path.GetFileName(strArquivoNFeProc);

                                                TFunctions.ExecutaUniDanfe(strArquivoDist, oLerXml.oDadosNfe.dEmi, Empresas.Configuracoes[emp]);
                                            }
                                            catch (Exception ex)
                                            {
                                                Auxiliar.WriteLog("TaskCTeRetRecepcao: " + ex.Message, false);
                                            }
                                        }
                                        break;

                                    case "301": //NFe Denegada - Irregularidade fiscal do emitente
                                        if (File.Exists(strArquivoNFe))
                                        {
                                            //Ler o XML para pegar a data de emissão para criar a pasta dos XML´s Denegados
                                            oLerXml.Cte(strArquivoNFe);

                                            //Mover a NFE da pasta de NFE em processamento para NFe Denegadas
                                            TFunctions.MoverArquivo(strArquivoNFe, PastaEnviados.Denegados, oLerXml.oDadosNfe.dEmi);
                                            ///
                                            /// existe DACTE de CTe denegado???
                                            /// 
                                            try
                                            {
                                                var strArquivoDist = Empresas.Configuracoes[emp].PastaXmlEnviado + "\\" +
                                                                        PastaEnviados.Denegados.ToString() + "\\" +
                                                                        Empresas.Configuracoes[emp].DiretorioSalvarComo.ToString(oLerXml.oDadosNfe.dEmi) +
                                                                        Functions.ExtrairNomeArq(strArquivoNFe, Propriedade.ExtEnvio.Cte) + Propriedade.ExtRetorno.Den;

                                                TFunctions.ExecutaUniDanfe(strArquivoDist, oLerXml.oDadosNfe.dEmi, Empresas.Configuracoes[emp]);
                                            }
                                            catch (Exception ex)
                                            {
                                                Auxiliar.WriteLog("TaskCTeRetRecepcao: " + ex.Message, false);
                                            }
                                        }
                                        break;

                                    case "302": //NFe Denegada - Irregularidade fiscal do remetente
                                        goto case "301";

                                    case "303": //NFe Denegada - Irregularidade fiscal do destinatário
                                        goto case "301";

                                    case "304": //NFe Denegada - Irregularidade fiscal do expedidor
                                        goto case "301";

                                    case "305": //NFe Denegada - Irregularidade fiscal do recebedor
                                        goto case "301";

                                    case "306": //NFe Denegada - Irregularidade fiscal do tomador
                                        goto case "301";

                                    case "110": //NFe Denegada - Não sei quando ocorre este, mas descobrir ele no manual então estou incluindo. Wandrey 20/10/2009
                                        goto case "301";

                                    default: //NFe foi rejeitada
                                        //Mover o XML da NFE a pasta de XML´s com erro
                                        oAux.MoveArqErro(strArquivoNFe);
                                        break;
                                }

                                //Deletar a NFE do arquivo de controle de fluxo
                                fluxoNFe.ExcluirNfeFluxo(strChaveNFe);
                                break;
                            }
                        }
                        break;
                    #endregion

                    #region Qualquer outro tipo de status que não for os acima relacionados, vai tirar a nota fiscal do fluxo.
                    default:
                        //Qualquer outro tipo de rejeião vou tirar todas as notas do lote do fluxo, pois se o lote foi rejeitado, todas as notas fiscais também foram
                        //De acordo com o manual de integração se o status do lote não for 104, tudo foi rejeitado. Wandrey 20/07/2010

                        //Vou retirar as notas do fluxo pelo recibo
                        if (nRec != string.Empty)
                        {
                            fluxoNFe.ExcluirNfeFluxoRec(nRec.Trim());
                        }

                        break;
                    #endregion
                }
            }
        }
        #endregion
    }
}
