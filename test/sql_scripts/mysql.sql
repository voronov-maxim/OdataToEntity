-- MySQL Workbench Forward Engineering

SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0;
SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;
SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- -----------------------------------------------------
-- Schema dbo
-- -----------------------------------------------------
DROP SCHEMA IF EXISTS `dbo` ;

-- -----------------------------------------------------
-- Schema dbo
-- -----------------------------------------------------
CREATE SCHEMA IF NOT EXISTS `dbo` DEFAULT CHARACTER SET utf8 ;
USE `dbo` ;

-- -----------------------------------------------------
-- Table `Sex`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `Sex` ;

CREATE TABLE IF NOT EXISTS `Sex` (
  `Id` INT NOT NULL,
  `Name` VARCHAR(128) NOT NULL,
  PRIMARY KEY (`Id`))
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `Customers`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `Customers` ;

CREATE TABLE IF NOT EXISTS `Customers` (
  `Address` VARCHAR(256) NULL,
  `Country` CHAR(2) NOT NULL,
  `Id` INT NOT NULL,
  `Name` VARCHAR(128) NOT NULL,
  `Sex` INT NULL,
  PRIMARY KEY (`Country`, `Id`),
  INDEX `FK_Customers_Sex_idx` (`Sex` ASC) VISIBLE,
  CONSTRAINT `FK_Customers_Sex`
    FOREIGN KEY (`Sex`)
    REFERENCES `Sex` (`Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `OrderStatus`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `OrderStatus` ;

CREATE TABLE IF NOT EXISTS `OrderStatus` (
  `Id` INT NOT NULL,
  `Name` VARCHAR(128) NOT NULL,
  PRIMARY KEY (`Id`))
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `Orders`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `Orders` ;

CREATE TABLE IF NOT EXISTS `Orders` (
  `AltCustomerCountry` CHAR(2) NULL,
  `AltCustomerId` INT NULL,
  `CustomerCountry` CHAR(2) NOT NULL,
  `CustomerId` INT NOT NULL,
  `Date` TIMESTAMP NULL,
  `Dummy` INT NULL,
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(256) NOT NULL,
  `Status` INT NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `FK_Orders_AltCustomers_idx` (`AltCustomerCountry` ASC, `AltCustomerId` ASC) VISIBLE,
  INDEX `FK_Orders_Customers_idx` (`CustomerCountry` ASC, `CustomerId` ASC) VISIBLE,
  INDEX `FK_Orders_OrderStatus_idx` (`Status` ASC) VISIBLE,
  CONSTRAINT `FK_Orders_AltCustomers`
    FOREIGN KEY (`AltCustomerCountry` , `AltCustomerId`)
    REFERENCES `Customers` (`Country` , `Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION,
  CONSTRAINT `FK_Orders_Customers`
    FOREIGN KEY (`CustomerCountry` , `CustomerId`)
    REFERENCES `Customers` (`Country` , `Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION,
  CONSTRAINT `FK_Orders_OrderStatus`
    FOREIGN KEY (`Status`)
    REFERENCES `OrderStatus` (`Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `OrderItems`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `OrderItems` ;

CREATE TABLE IF NOT EXISTS `OrderItems` (
  `Count` INT NULL,
  `Id` INT NOT NULL AUTO_INCREMENT,
  `OrderId` INT NOT NULL,
  `Price` DECIMAL(18,2) NULL,
  `Product` VARCHAR(256) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `FK_OrderItem_Order_idx` (`OrderId` ASC) VISIBLE,
  CONSTRAINT `FK_OrderItem_Order`
    FOREIGN KEY (`OrderId`)
    REFERENCES `Orders` (`Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `ShippingAddresses`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `ShippingAddresses` ;

CREATE TABLE IF NOT EXISTS `ShippingAddresses` (
  `OrderId` INT NOT NULL,
  `Id` INT NOT NULL,
  `Address` VARCHAR(256) NOT NULL,
  PRIMARY KEY (`OrderId`, `Id`),
  CONSTRAINT `FK_ShippingAddresses_Order`
    FOREIGN KEY (`OrderId`)
    REFERENCES `Orders` (`Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `CustomerShippingAddress`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `CustomerShippingAddress` ;

CREATE TABLE IF NOT EXISTS `CustomerShippingAddress` (
  `CustomerCountry` CHAR(2) NOT NULL,
  `CustomerId` INT NOT NULL,
  `ShippingAddressOrderId` INT NOT NULL,
  `ShippingAddressId` INT NOT NULL,
  PRIMARY KEY (`CustomerCountry`, `CustomerId`, `ShippingAddressOrderId`, `ShippingAddressId`),
  INDEX `FK_CustomerShippingAddress_ShippingAddresses_idx` (`ShippingAddressOrderId` ASC, `ShippingAddressId` ASC) VISIBLE,
  CONSTRAINT `FK_CustomerShippingAddress_Customers`
    FOREIGN KEY (`CustomerCountry` , `CustomerId`)
    REFERENCES `Customers` (`Country` , `Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION,
  CONSTRAINT `FK_CustomerShippingAddress_ShippingAddresses`
    FOREIGN KEY (`ShippingAddressOrderId` , `ShippingAddressId`)
    REFERENCES `ShippingAddresses` (`OrderId` , `Id`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `Categories`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `Categories` ;

CREATE TABLE IF NOT EXISTS `Categories` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(128) NOT NULL,
  `ParentId` INT NULL,
  `DateTime` DATETIME NULL,
  PRIMARY KEY (`Id`),
  INDEX `FK_Categories_Categories_idx` (`ParentId` ASC) VISIBLE,
  CONSTRAINT `FK_Categories_Categories`
    FOREIGN KEY (`ParentId`)
    REFERENCES `Categories` (`Id`)
    ON DELETE CASCADE
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `ManyColumns`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `ManyColumns` ;

CREATE TABLE IF NOT EXISTS `ManyColumns` (
  `Column01` INT NOT NULL,
  `Column02` INT NOT NULL,
  `Column03` INT NOT NULL,
  `Column04` INT NOT NULL,
  `Column05` INT NOT NULL,
  `Column06` INT NOT NULL,
  `Column07` INT NOT NULL,
  `Column08` INT NOT NULL,
  `Column09` INT NOT NULL,
  `Column10` INT NOT NULL,
  `Column11` INT NOT NULL,
  `Column12` INT NOT NULL,
  `Column13` INT NOT NULL,
  `Column14` INT NOT NULL,
  `Column15` INT NOT NULL,
  `Column16` INT NOT NULL,
  `Column17` INT NOT NULL,
  `Column18` INT NOT NULL,
  `Column19` INT NOT NULL,
  `Column20` INT NOT NULL,
  `Column21` INT NOT NULL,
  `Column22` INT NOT NULL,
  `Column23` INT NOT NULL,
  `Column24` INT NOT NULL,
  `Column25` INT NOT NULL,
  `Column26` INT NOT NULL,
  `Column27` INT NOT NULL,
  `Column28` INT NOT NULL,
  `Column29` INT NOT NULL,
  `Column30` INT NOT NULL,
  PRIMARY KEY (`Column01`))
ENGINE = InnoDB;

USE `dbo` ;

-- -----------------------------------------------------
-- Placeholder table for view `OrderItemsView`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `OrderItemsView` (`Name` INT, `Product` INT);

-- -----------------------------------------------------
-- procedure ResetDb
-- -----------------------------------------------------

USE `dbo`;
DROP procedure IF EXISTS `ResetDb`;

DELIMITER $$
USE `dbo`$$
create procedure ResetDb ()
begin
	delete from CustomerShippingAddress;
	delete from ShippingAddresses;
	delete from OrderItems;
	delete from Orders;
	delete from Customers;
	delete from Categories;

	alter table OrderItems auto_increment = 0;
	alter table Orders auto_increment = 0;
	alter table Categories auto_increment = 0;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- procedure ResetManyColumns
-- -----------------------------------------------------

USE `dbo`;
DROP procedure IF EXISTS `ResetManyColumns`;

DELIMITER $$
USE `dbo`$$
create procedure ResetManyColumns ()
begin
	delete from ManyColumns;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- procedure GetOrders
-- -----------------------------------------------------

USE `dbo`;
DROP procedure IF EXISTS `GetOrders`;

DELIMITER $$
USE `dbo`$$
create procedure GetOrders (id int, name varchar(256), status int)
begin
	if id is null and name is null and status is null then
		select * from Orders;
	elseif not id is null then
		select * from Orders o where o.Id = id;
	elseif not name is null then
		select * from Orders o where o.Name like concat('%', name, '%');
    elseif not status is null then
		select * from Orders o where o.Status = status;
	end if;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- procedure TableFunction
-- -----------------------------------------------------

USE `dbo`;
DROP procedure IF EXISTS `TableFunction`;

DELIMITER $$
USE `dbo`$$
create procedure TableFunction ()
begin
	select * from Orders;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- procedure TableFunctionWithParameters
-- -----------------------------------------------------

USE `dbo`;
DROP procedure IF EXISTS `TableFunctionWithParameters`;

DELIMITER $$
USE `dbo`$$
create procedure TableFunctionWithParameters (id int, name varchar(256), status int)
begin
	select * from Orders o where o.Id = id or o.Name like concat('%', name, '%') or o.Status = status;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- function ScalarFunction
-- -----------------------------------------------------

USE `dbo`;
DROP function IF EXISTS `ScalarFunction`;

DELIMITER $$
USE `dbo`$$
create function ScalarFunction () returns integer
reads sql data deterministic
begin
	declare zcount int;
	select count(*) into zcount from Orders;
    return zcount;
end$$

DELIMITER ;

-- -----------------------------------------------------
-- View `OrderItemsView`
-- -----------------------------------------------------
DROP TABLE IF EXISTS `OrderItemsView`;
DROP VIEW IF EXISTS `OrderItemsView` ;
USE `dbo`;
create  OR REPLACE view OrderItemsView as
	select o.Name, i.Product from Orders o inner join OrderItems i on o.Id = i.OrderId;

SET SQL_MODE=@OLD_SQL_MODE;
SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS;
SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS;

-- -----------------------------------------------------
-- Data for table `Sex`
-- -----------------------------------------------------
START TRANSACTION;
USE `dbo`;
INSERT INTO `Sex` (`Id`, `Name`) VALUES (0, 'Male');
INSERT INTO `Sex` (`Id`, `Name`) VALUES (1, 'Female');

COMMIT;


-- -----------------------------------------------------
-- Data for table `OrderStatus`
-- -----------------------------------------------------
START TRANSACTION;
USE `dbo`;
INSERT INTO `OrderStatus` (`Id`, `Name`) VALUES (0, 'Unknown');
INSERT INTO `OrderStatus` (`Id`, `Name`) VALUES (1, 'Processing');
INSERT INTO `OrderStatus` (`Id`, `Name`) VALUES (2, 'Shipped');
INSERT INTO `OrderStatus` (`Id`, `Name`) VALUES (3, 'Delivering');
INSERT INTO `OrderStatus` (`Id`, `Name`) VALUES (4, 'Cancelled');

COMMIT;

