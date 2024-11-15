﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using warehub.model;
using warehub.services.interfaces;

namespace warehub.db
{
    public class ProductRepository
    {
        private readonly CRUDService _cRUDService;
        public ProductRepository()
        {
            MySqlConnection connection = DbConnection.GetConnection();
            _cRUDService = new CRUDService(connection);
        }
        

        public GenericResponseDTO<Product> Add(Product product)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@name", product.Name },
                { "@price", product.Price },
                { "@id", product.Id }
            };
            bool status = _cRUDService.Create("INSERT INTO Products (Name, Price, Id) VALUES (@name, @price, @id)", parameters);
            var returnObject = new GenericResponseDTO<Product>(product)
            {
                IsSuccess = status
            };
            return returnObject;
        }

        public GenericResponseDTO<Guid> Delete(Guid id)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@id", id }
            };
            bool status = _cRUDService.Delete("DELETE FROM Products WHERE ID = @id", parameters);
            var returnObject = new GenericResponseDTO<Guid>(id)
            {
                IsSuccess = status
            };
            return returnObject;
        }

        public GenericResponseDTO<List<Product>> GetAll()
        {
            var parameters = new Dictionary<string, object>
            {
             };
            (bool status, List<Dictionary<string, object>> products) = _cRUDService.Read("SELECT * FROM Products", parameters);  
            List<Product> listOfProducts = ConvertToProducts(products);
            var returnObject = new GenericResponseDTO<List<Product>>(listOfProducts)
            {
                IsSuccess = status
            };
            return returnObject;
        }

        public GenericResponseDTO<Product> GetById(Guid id)
        {
            string query = "SELECT * FROM Products WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", id }
            };

            // Execute the Read method with the corrected query
            (bool status, List<Dictionary<string, object>> products) = _cRUDService.Read(query, parameters);
            List<Product> listOfProducts = ConvertToProducts(products);
            
            var product = listOfProducts.FirstOrDefault(p => p.Id == id);
            var returnObject = new GenericResponseDTO<Product>(product)
            {
                IsSuccess = status
            };
            return returnObject;
        }

        public GenericResponseDTO<Product> Update(Product product)
        {
            var updateParams = new Dictionary<string, object>
            {
                { "@name", product.Name },
                { "@price", product.Price },
                { "@id", product.Id }
            };
            bool status = _cRUDService.Update("UPDATE Products SET Name = @name, Price = @price WHERE ID = @id", updateParams);

            var returnObject = new GenericResponseDTO<Product>(product)
            {
                IsSuccess = status
            };
            return returnObject;
        }

        // DELETE when factory is implementet
        public List<Product> ConvertToProducts(List<Dictionary<string, object>> products)
        {
            var productList = new List<Product>();

            foreach (var productDict in products)
            {
                Guid id;
                if (productDict.ContainsKey("ID") && productDict["ID"] is Guid)
                {
                    id = (Guid)productDict["ID"];
                }
                else
                {
                    continue;
                }

                string name;
                if (productDict.ContainsKey("Name") && productDict["Name"] is string)
                {
                    name = (string)productDict["Name"];
                }
                else
                {
                    continue;
                }

                int price;
                if (productDict.ContainsKey("Price") && productDict["Price"] is int)
                {
                    price = (int)productDict["Price"];
                }
                else
                {
                    continue;
                }
                // Create new Product instance with parsed values
                var product = ProductFactory.CreateProduct(id, name, price);
                productList.Add(product);
            }

            return productList;
        }
    }
}


